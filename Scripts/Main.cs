using System;
using Godot;
using TrashPcTycoon.UI;

namespace TrashPcTycoon;

/// <summary>
/// Корневая сцена: связывает гараж, свалку, коробку AliExpress (Модуль 1) и
/// стол сборки, молоток, майонез, датчики и стресс-тест (Модуль 2).
/// Один указатель — три режима: обычный (тап = рейкаст, тяга = камера),
/// перетаскивание детали на стол и режим майонеза (свайп по процессору).
/// </summary>
public partial class Main : Node3D
{
    private readonly Random _rng = new();

    private GameState _state = null!;
    private MainUI _ui = null!;
    private IsoCameraRig _cameraRig = null!;
    private TrashHeap _heap = null!;
    private DeliveryBox _deliveryBox = null!;
    private BuildController _build = null!;
    private HeatSystem _heat = null!;
    private AndroidSensors _sensors = null!;
    private ClientController _client = null!;
    private SoundSystem _sound = null!;
    private GarageBuilder _garage = null!;
    private float _challengePeak;
    private float _eventTimer = 45f;
    private float _blackoutTimer;
    private float _catTimer;
    private bool _fleaBonus;

    private enum ClientStage { None, Intro, Request, WaitingBuild, ChallengePassed }
    private ClientStage _clientStage = ClientStage.None;
    private ClientData? _clientData;
    private bool _challengeRunning;
    private float _nextClientTimer = -1f;
    private float _openCaseCooldown;

    private PcPart? _pendingAliPart;
    private bool _deliveryInProgress;
    private bool _gameStarted;
    private bool _pasteMode;
    private bool _blowHeld;

    private Vector2 _pressPosition;
    private bool _pointerDown;
    private bool _dragging;
    private ulong _lastTapMsec;

    private const float DragThresholdPx = 24.0f;

    public override void _Ready()
    {
        BuildEnvironment();

        _state = GetNode<GameState>("/root/GameState");
        _ui = GetNode<MainUI>("UI");
        _cameraRig = GetNode<IsoCameraRig>("CameraRig");
        _heap = GetNode<TrashHeap>("TrashHeap");
        _deliveryBox = GetNode<DeliveryBox>("DeliveryBox");
        _build = GetNode<BuildController>("BuildController");
        _heat = GetNode<HeatSystem>("HeatSystem");
        _sensors = GetNode<AndroidSensors>("AndroidSensors");
        _client = GetNode<ClientController>("Client");
        _sound = GetNode<SoundSystem>("SoundSystem");
        _garage = GetNode<GarageBuilder>("Garage");

        _heat.Bind(_build);

        _ui.Bind(_state);
        _ui.PlayPressed += OnPlayPressed;
        _ui.DigPressed += DigAtHeap;
        _ui.BuyBoxPressed += OnBuyBoxPressed;
        _ui.OpenBoxPressed += OpenDeliveryBox;
        _ui.SellPartPressed += OnSellPartPressed;
        _ui.InstallPartPressed += OnInstallPartPressed;
        _ui.HammerPressed += OnHammerPressed;
        _ui.RemoveSelectedPressed += OnRemoveSelectedPressed;
        _ui.PasteModePressed += OnPasteModePressed;
        _ui.BlowButtonDown += () => _blowHeld = true;
        _ui.BlowButtonUp += () => _blowHeld = false;
        _ui.StartTestPressed += OnStartTestPressed;
        _ui.StopTestPressed += () =>
        {
            bool wasChallenge = _challengeRunning;
            _challengeRunning = false;
            _heat.StopTest(wasChallenge
                ? "Запуск Киберпанка прерван вручную. Клиент делает вид, что ничего не видел."
                : "Тест остановлен вручную. ПК выдохнул с облегчением.");
        };
        _ui.SavePressed += () => SaveGame(announce: true);
        _ui.BuyToolPressed += OnBuyToolPressed;
        _ui.SummonClientPressed += () =>
        {
            if (_clientStage == ClientStage.None)
            {
                SpawnClient();
            }
        };
        _ui.ClientNextPressed += () =>
        {
            if (_clientStage == ClientStage.Intro)
            {
                _clientStage = ClientStage.Request;
                _client.Say(_clientData != null ? $"{_clientData.GameTitle}. Ну пожалуйста." : "Запусти игру. Ну пожалуйста.");
                UpdateClientUi();
            }
        };
        _ui.AcceptOrderPressed += () =>
        {
            if (_clientStage != ClientStage.Request)
            {
                return;
            }

            _clientStage = ClientStage.WaitingBuild;
            _client.Say("Жду! Только не как почта, ладно?");
            _ui.SetMessage("Заказ принят. Собирай ПК, мажь майонез, продувай пыль — потом «Запустить Киберпанк» в панели клиента.");
            UpdateClientUi();
        };
        _ui.LaunchCyberpunkPressed += OnLaunchCyberpunk;
        _ui.SellPcPressed += OnSellPc;
        _ui.RejectClientPressed += () =>
        {
            if (_clientData == null)
            {
                return;
            }

            _client.Leave(false);
            _clientData = null;
            _clientStage = ClientStage.None;
            _nextClientTimer = 25f;
            _state.AddReputation(-2);
            _ui.SetMessage("Клиент ушёл мечтать дальше. Репутация -2: сарафанное радио всё слышит.");
            UpdateClientUi();
        };
        _ui.BuyGaragePressed += OnBuyGaragePressed;
        _ui.OpenCasePressed += () =>
        {
            if (!_heat.IsRunning)
            {
                _ui.SetMessage("Корпус и так открыт для всех ветров. Охлаждать нечего.");
                return;
            }

            if (_openCaseCooldown > 0f)
            {
                _ui.SetMessage($"Крышка ещё «отдыхает» {_openCaseCooldown:0} сек. Постучи по столу от нетерпения.");
                return;
            }

            _openCaseCooldown = 20f;
            _heat.ApplyInstantCooling(12f);
            _sound.PlayCreak();
            _ui.SetMessage("Крышка сорвана! -12°C. Где-то в углу заплакал один гарантийный стикер.");
        };

        _heat.TemperatureChanged += (celsius, left, running) => _ui.SetTemperature(celsius, left, running);
        _heat.CriticalTemperature += celsius =>
        {
            _ui.SetMessage($"КРИТИЧНО: {celsius:0}°C! ДУЙ В ТЕЛЕФОН, КАК В КАРТРИДЖ! Телефон уже вибрирует от ужаса.");
        };
        _heat.TestFinished += (survived, summary) =>
        {
            _ui.SetMessage(summary);
            RefreshBuildUi();
        };

        _sensors.ShakeDetected += OnShakeDetected;
        _sensors.BlowStateChanged += blowing =>
        {
            if (blowing)
            {
                _ui.SetMessage("Продувка пошла! Пыль летит из ПК, как из старого пылесоса наоборот.");
            }
        };

        _ui.ShowMenu();
        _ui.SetSensorStatus(_sensors.Status);
        _ui.SetMessage("Нажми «Играть». Свалка ждёт, стол сборки пылится, молоток скучает.");

        GD.Print("Trash-PC Tycoon 3D: Модули 1-2 загружены. Гараж открыт, молоток на месте.");
    }

    public override void _Process(double delta)
    {
        if (_gameStarted)
        {
            _eventTimer -= (float)delta;
            if (_eventTimer <= 0f)
            {
                _eventTimer = 50f + (float)_rng.NextDouble() * 35f;
                RollRandomEvent();
            }

            if (_blackoutTimer > 0f)
            {
                _blackoutTimer -= (float)delta;
                if (_blackoutTimer <= 0f)
                {
                    _garage.SetBlackout(false);
                    _ui.SetMessage("Свет дали. Лампочка мигнула и сделала вид, что так и было.");
                }
            }

            if (_catTimer > 0f)
            {
                _catTimer -= (float)delta;
                if (_catTimer <= 0f)
                {
                    _garage.SetCatVisible(false);
                }
            }
        }

        bool blowing = _sensors.IsBlowing || _blowHeld;
        _heat.SetExternalCooling(blowing);
        if (blowing && _gameStarted)
        {
            _build.ApplyBlow(delta);
        }

        if (_ui.IsBuildPanelOpen)
        {
            _ui.SetMicLevel(_sensors.Loudness, blowing);
            RefreshBuildUi();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_gameStarted)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse:
                if (mouse.Pressed)
                {
                    BeginPointer(mouse.Position);
                }
                else
                {
                    EndPointer(mouse.Position);
                }
                break;

            case InputEventMouseMotion motion when _pointerDown:
                MovePointer(motion.Position, motion.Relative);
                break;

            case InputEventScreenTouch touch:
                if (touch.Pressed)
                {
                    BeginPointer(touch.Position);
                }
                else
                {
                    EndPointer(touch.Position);
                }
                break;

            case InputEventScreenDrag drag when _pointerDown:
                MovePointer(drag.Position, drag.Relative);
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                _cameraRig.Zoom(-1.0f);
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                _cameraRig.Zoom(1.0f);
                break;
        }
    }

    // ---------- Модуль 3: клиенты, инструменты, сохранения ----------

    private void SpawnClient()
    {
        if (_clientStage != ClientStage.None)
        {
            return;
        }

        float budgetMult = GarageCatalog.ClientBudgetMultiplier(_state.GarageLevel);
        _clientData = ClientCatalog.CreateRandom(_rng, _state.GarageLevel, budgetMult);
        _clientStage = ClientStage.Intro;
        _nextClientTimer = -1f;
        _client.Arrive(_clientData);
        _sound.PlayCreak();
        _ui.SetMessage($"В гараж зашёл клиент: {_clientData.Name} ({_clientData.Archetype}). Открой панель «Клиент» — у него мечта и копилка.");
        UpdateClientUi();
    }

    private int ComputeSalePriceRub()
    {
        float repBonus = 1.0f + (_state.Reputation / 25) * 0.05f;
        float raw = (_build.GetTotalValueRub() * 1.6f + 250f)
            * GarageCatalog.SalePriceMultiplier(_state.GarageLevel) * repBonus;
        return (int)raw;
    }

    private void UpdateClientUi()
    {
        string stats = $"Продано ПК: {_state.ComputersSold} • Заработано всего: {_state.TotalEarnedRub} ₽";
        switch (_clientStage)
        {
            case ClientStage.None:
                _ui.SetClientDialogue("Клиента нет",
                    "У прилавка пусто. Клиенты приходят сами (первый — секунд через шесть после старта) или зови кнопкой «Клиент» вверху.",
                    stats, false, false, false, false, false);
                break;

            case ClientStage.Intro when _clientData != null:
                _ui.SetClientDialogue(_clientData.Name, _clientData.IntroLine,
                    $"Бюджет копилки: около {_clientData.BudgetRub} ₽. {stats}",
                    true, false, false, false, true);
                break;

            case ClientStage.Request when _clientData != null:
                _ui.SetClientDialogue(_clientData.Name, _clientData.RequestLine,
                    $"Бюджет: {_clientData.BudgetRub} ₽. {stats}",
                    false, true, false, false, true);
                break;

            case ClientStage.WaitingBuild when _clientData != null:
                var missing = _build.GetMissingEssentials();
                string status = missing.Count == 0
                    ? $"Сборка готова! Цена для клиента: {ComputeSalePriceRub()} ₽ (бюджет {_clientData.BudgetRub} ₽). Запускай Киберпанк."
                    : $"Собираем… не хватает: {string.Join(", ", missing)}. Цена будет около {ComputeSalePriceRub()} ₽.";
                _ui.SetClientDialogue(_clientData.Name, ClientCatalog.WaitingLine, status,
                    false, false, missing.Count == 0 && !_heat.IsRunning, false, true);
                break;

            case ClientStage.ChallengePassed when _clientData != null:
                _ui.SetClientDialogue(_clientData.Name,
                    "ОНО РАБОТАЕТ. Киберпанк. У меня. Скажи цену, только не громко — сердце стучит, как твой HDD.",
                    $"Цена: {ComputeSalePriceRub()} ₽, бюджет клиента {_clientData.BudgetRub} ₽. Жми «Продать ПК».",
                    false, false, false, true, false);
                break;
        }
    }

    private void OnLaunchCyberpunk()
    {
        if (_clientStage != ClientStage.WaitingBuild)
        {
            return;
        }

        if (_heat.StartChallenge(out string error))
        {
            _challengeRunning = true;
            _sound.SetFan(true);
            _client.Say("Это он? Это КИБЕРПАНК?!");
            _ui.OpenBuildPanel();
            _ui.SetMessage("КИБЕРПАНК ЗАПУЩЕН ПРИ КЛИЕНТЕ! 60 секунд: дуй в микрофон, открывай корпус, не дай дойти до 100°C!");
        }
        else
        {
            _ui.SetMessage(error);
        }

        UpdateClientUi();
    }

    private void OnTestFinished(bool survived, string summary)
    {
        _sound.SetFan(false);
        bool wasChallenge = _challengeRunning;
        _challengeRunning = false;

        if (wasChallenge && survived && _clientData != null)
        {
            _challengePeak = _heat.PeakTemperature;
            _clientStage = ClientStage.ChallengePassed;
            _client.Say("ШЕСТЬДЕСЯТ СЕКУНД! ОНО ЖИВО!");
            _sound.PlayCash();
            _ui.SetMessage(summary + " Теперь жми «Продать ПК» в панели клиента.");
        }
        else if (wasChallenge && !survived && _clientData != null)
        {
            _sound.PlayExplosion();
            _sound.PlayFail();
            PcPart? burned = _build.BurnRandomPart();
            _state.AddReputation(-10);
            _client.Leave(false);
            _clientData = null;
            _clientStage = ClientStage.None;
            _nextClientTimer = 30f;
            _ui.SetMessage(summary + (burned != null
                ? $" Сгорела деталь: «{burned.Name}». Клиент ушёл к другому дяде. Репутация -10."
                : " Клиент ушёл к другому дяде. Репутация -10."));
            SaveGame();
        }
        else
        {
            if (!survived)
            {
                _sound.PlayExplosion();
            }

            _ui.SetMessage(summary);
        }

        RefreshBuildUi();
        UpdateClientUi();
    }

    private void OnSellPc()
    {
        if (_clientStage != ClientStage.ChallengePassed || _clientData == null)
        {
            return;
        }

        int price = ComputeSalePriceRub();
        int paid = price <= _clientData.BudgetRub
            ? price
            : _clientData.BudgetRub + _rng.Next(0, 81);
        string note = price <= _clientData.BudgetRub
            ? $"Клиент заплатил {paid} ₽, как договаривались. Даже сдачу не пересчитывал."
            : $"Цена {price} ₽ оказалась выше копилки, клиент отдал всё: {paid} ₽ (с мелочью из карманов).";

        int repDelta;
        if (_clientData.MaxTempCelsius.HasValue && _challengePeak > _clientData.MaxTempCelsius.Value)
        {
            paid = (int)(paid * 0.7f);
            repDelta = 2;
            note += $" Но пик был {_challengePeak:0}°C при лимите {_clientData.MaxTempCelsius.Value:0}°C — шумно вышло, поэтому -30% и репутация всего +2.";
        }
        else if (_challengePeak <= 75f)
        {
            repDelta = 9;
            note += $" Пик всего {_challengePeak:0}°C — тихо и холодно, клиент в восторге. Репутация +9!";
        }
        else
        {
            repDelta = 6;
            note += " Репутация +6.";
        }

        _state.AddReputation(repDelta);

        _state.AddMoney(paid);
        _state.RegisterSale();
        _build.ClearBuild();
        _sound.PlayCash();
        Input.VibrateHandheld(60);
        _client.Leave(true);
        _clientData = null;
        _clientStage = ClientStage.None;
        _nextClientTimer = 25f;
        _ui.SetMessage($"ПК ПРОДАН! {note} Стол пуст — время собирать следующее чудо.");
        SaveGame();
        RefreshBuildUi();
        UpdateClientUi();
    }

    private void OnBuyToolPressed(string toolId)
    {
        ToolsCatalog.ToolDef? def = ToolsCatalog.Find(toolId);
        if (def == null)
        {
            return;
        }

        if (_state.OwnsTool(toolId))
        {
            _ui.SetMessage($"«{def.Name}» уже твой. Два молотка — это уже арсенал, а не гараж.");
            return;
        }

        if (!_state.BuyTool(toolId, def.PriceRub))
        {
            _ui.SetMessage($"На «{def.Name}» не хватает. Копай, продавай, возвращайся — классический бизнес-план.");
            return;
        }

        ApplyToolEffects();
        _sound.PlayPop();
        _ui.SetMessage($"Куплено: {def.Name}! {def.Description}");
        SaveGame();
    }

    private void ApplyToolEffects()
    {
        _build.HammerHitsRequired = _state.OwnsTool(ToolsCatalog.BetterHammer) ? 1 : 2;
        _build.BlowDustMultiplier = _state.OwnsTool(ToolsCatalog.Vacuum) ? 2.0f : 1.0f;
        _build.HasRealPaste = _state.OwnsTool(ToolsCatalog.RealPaste);
        _heat.PassiveCoolingPerSecond = _state.OwnsTool(ToolsCatalog.DeskFan) ? 1.6f : 0f;
    }

    private void SaveGame(bool announce = false)
    {
        SaveSystem.Save(_state, _build);
        if (announce)
        {
            _sound.PlayPop();
            _ui.SetMessage("Прогресс сохранён: деньги, детали, стол сборки и инструменты — всё записано в тетрадку гаража.");
        }
    }

    private void LoadGame()
    {
        SaveSystem.SaveData? data = SaveSystem.Load();
        if (data == null)
        {
            return;
        }

        var inventory = new System.Collections.Generic.List<PcPart>();
        foreach (SaveSystem.PartDto dto in data.Inventory)
        {
            inventory.Add(SaveSystem.FromDto(dto));
        }

        _state.Restore(data.Money, inventory, data.Tools, data.ComputersSold, data.TotalEarnedRub, data.Reputation, data.GarageLevel);
        foreach (SaveSystem.InstalledDto installed in data.Installed)
        {
            _build.RestoreInstalled(SaveSystem.FromDto(installed.Part), installed.NeedsHammer,
                installed.Hammered, installed.HammerHits, installed.Dust01, installed.Paste01);
        }

        _garage.ApplyLevel(_state.GarageLevel);
        GD.Print("Trash-PC Tycoon: сейв загружен.");
    }

    private void OnBuyGaragePressed(int targetLevel)
    {
        GarageCatalog.LevelDef? next = GarageCatalog.Next(_state.GarageLevel);
        if (next == null || next.Level != targetLevel)
        {
            return;
        }

        if (!_state.BuyGarageUpgrade(targetLevel, next.PriceRub))
        {
            _ui.SetMessage($"На «{next.Name}» не хватает ({next.PriceRub} ₽). Продавай ПК, а не мечты. Точнее — и то, и другое.");
            return;
        }

        _garage.ApplyLevel(_state.GarageLevel);
        _sound.PlayCash();
        _ui.SetMessage($"ГАРАЖ УЛУЧШЕН: {next.Name}! {next.Description}");
        SaveGame();
    }

    private void RollRandomEvent()
    {
        switch (_rng.Next(6))
        {
            case 0:
                PcPart gift = PartCatalog.CreateDumpFind(_rng);
                _state.AddPart(gift);
                _sound.PlayPop();
                _ui.SetMessage($"Событие: зашёл дядя Витя, оставил «{gift.Name}» со словами «мне уже без надобности». Лежит в инвентаре.");
                SaveGame();
                break;
            case 1:
                _blackoutTimer = 15f;
                _garage.SetBlackout(true);
                _ui.SetMessage("Событие: ОТКЛЮЧИЛИ СВЕТ на 15 секунд. Лампочка в отпуске. Копай на ощупь, как предки.");
                break;
            case 2:
                _catTimer = 20f;
                _garage.SetCatVisible(true);
                _build.AddDustToAll(0.15f);
                _ui.SetMessage("Событие: гаражный кот Барсик лёг спать на тёплый ПК. Уют +100, пыль на деталях +15%.");
                break;
            case 3:
                _fleaBonus = true;
                _ui.SetMessage("Событие: мимо едет щедрый барахольщик! Следующая сдача детали из инвентаря — за +50%.");
                break;
            case 4:
                _build.AddDustToAll(0.20f);
                _ui.SetMessage("Событие: крыша протекла прямо на стол сборки. Детали +20% пыли и жизненного опыта.");
                break;
            default:
                int coins = _rng.Next(30, 91);
                _state.AddMoney(coins);
                _ui.SetMessage($"Событие: в кармане старых джинсов на свалке нашлось {coins} ₽. Свалка платит тем, кто её любит.");
                SaveGame();
                break;
        }
    }

    // ---------- Модуль 1: свалка и магазин ----------

    private void OnPlayPressed()
    {
        _gameStarted = true;
        _sensors.StartMicrophone();
        _ui.ShowGame();
        _ui.SetSensorStatus(_sensors.Status);
        _ui.SetMessage("Тапай по куче мусора справа — там твой стартовый капитал. Буквально.");
    }

    private void DigAtHeap()
    {
        if (!_gameStarted)
        {
            return;
        }

        _heap.PlayDigEffect(_rng);
        int roll = _rng.Next(100);

        if (roll < 5)
        {
            PcPart epic = PartCatalog.CreateEpicFind(_rng);
            _state.AddPart(epic);
            ShowFoundPart(epic, _heap.GlobalPosition + new Vector3(0, 1.4f, 0));
            _sound.PlayCash();
            _state.AddReputation(1);
            _ui.SetMessage($"ЛЕГЕНДАРНАЯ НАХОДКА: {epic.Name}! {epic.Description} Репутация гаража +1: о тебе начинают говорить.");
            SaveGame();
        }
        else if (roll < 58)
        {
            PcPart part = PartCatalog.CreateDumpFind(_rng);
            _state.AddPart(part);
            ShowFoundPart(part, _heap.GlobalPosition + new Vector3(0, 1.1f, 0));
            _ui.SetMessage($"Выкопал: {part.Name}. {part.Description}");
        }
        else if (roll < 80)
        {
            _state.AddMoney(PartCatalog.ScrapRewardRub);
            _ui.SetMessage($"{PartCatalog.RandomScrapLine(_rng)} +{PartCatalog.ScrapRewardRub} ₽");
        }
        else
        {
            _ui.SetMessage(PartCatalog.RandomNothingLine(_rng));
        }
    }

    private void OnBuyBoxPressed()
    {
        if (_deliveryInProgress)
        {
            _ui.SetShopStatus("Одна коробка уже едет. Почта гаража работает на пределе (на одном человеке).");
            return;
        }

        if (_deliveryBox.HasUnopenedBox)
        {
            _ui.SetShopStatus("На верстаке уже лежит невскрытая коробка. Сначала загляни в неё.");
            return;
        }

        if (!_state.SpendMoney(PartCatalog.AliBoxPriceRub))
        {
            _ui.SetShopStatus("Не хватает денег. Иди копай свалку — классика жанра.");
            _ui.SetMessage("Кассы хватает только на мечты. Копай свалку и сдавай цветмет.");
            return;
        }

        _deliveryInProgress = true;
        _ui.SetBuyEnabled(false);
        _ui.SetShopStatus("Заказ принят! Трек-номер: «доверься». Коробка уже где-то между Китаем и твоим гаражом...");
        _ui.SetMessage("Коробка AliExpress заказана. Идёт пешком, судя по срокам.");

        GetTree().CreateTimer(1.8).Timeout += () =>
        {
            _deliveryInProgress = false;
            _pendingAliPart = PartCatalog.CreateAliPart(_rng);
            _deliveryBox.SpawnBox();
            _ui.SetBuyEnabled(true);
            _ui.SetOpenEnabled(true);
            _ui.SetShopStatus("Коробка доставлена на верстак! Тапни по ней в 3D или жми «Вскрыть коробку».");
            _ui.SetMessage("Посылка прибыла. Коробка подозрительно лёгкая. Как обычно.");
        };
    }

    private void OpenDeliveryBox()
    {
        if (_pendingAliPart == null)
        {
            _ui.SetShopStatus("Вскрывать нечего: коробки нет. Закажи одну, оптимист.");
            return;
        }

        PcPart part = _pendingAliPart;
        if (!_deliveryBox.OpenBox(part))
        {
            return;
        }

        _pendingAliPart = null;
        _state.AddPart(part);
        _ui.SetOpenEnabled(false);
        _ui.SetShopStatus($"Внутри: {part.Name}. {part.Description}");
        _ui.SetMessage($"Из коробки вылезло: {part.Name}. Оно добавлено в инвентарь.");
    }

    private void OnSellPartPressed(PcPart part)
    {
        if (!_state.RemovePart(part))
        {
            return;
        }

        int sellValue = _fleaBonus ? (int)(part.ValueRub * 1.5f) : part.ValueRub;
        if (_fleaBonus)
        {
            _fleaBonus = false;
        }

        _state.AddMoney(sellValue);
        _ui.SetMessage(_fleaBonus || sellValue != part.ValueRub
            ? $"Барахольщик проездом! Сдал «{part.Name}» за {sellValue} ₽ (+50%). Он даже не спросил, откуда это."
            : $"Сдал «{part.Name}» барахольщику за {sellValue} ₽. Он даже не спросил, откуда это.");
    }

    // ---------- Модуль 2: сборка ----------

    private void OnInstallPartPressed(PcPart part)
    {
        string message = _build.BeginPlacement(part);
        _ui.SetMessage(message);
        if (_build.IsPlacing)
        {
            _ui.ClosePanels();
        }

        RefreshBuildUi();
    }

    private void OnHammerPressed()
    {
        _ui.SetMessage(_build.HammerSelected());
        RefreshBuildUi();
    }

    private void OnRemoveSelectedPressed()
    {
        PcPart? removed = _build.RemoveSelected();
        if (removed == null)
        {
            _ui.SetMessage("Снимать нечего: сначала тапни по детали на столе.");
            return;
        }

        _state.AddPart(removed);
        _ui.SetMessage($"Снял «{removed.Name}» обратно в инвентарь. Без молотка, заметь. Прогресс!");
        RefreshBuildUi();
    }

    private void OnPasteModePressed()
    {
        _pasteMode = !_pasteMode;
        if (_pasteMode && !_build.Installed.ContainsKey(PartType.Cpu))
        {
            _pasteMode = false;
            _ui.SetMessage("Мазать нечего: процессора на столе нет. Майонез пока побудет просто майонезом.");
        }
        else if (_pasteMode)
        {
            _ui.SetMessage("Режим майонеза ВКЛ. Води пальцем прямо по процессору на столе. Щедро, как в столовой.");
        }

        _ui.SetPasteState(_build.CpuPaste01, _pasteMode);
        RefreshBuildUi();
    }

    private void OnStartTestPressed()
    {
        if (_heat.StartTest(out string error))
        {
            _sound.SetFan(true);
            _ui.SetMessage("ТЕСТ ЗАПУЩЕН! Вентилятор взвыл, HDD застучал, температура полезла вверх. Дуй, если запахнет жареным.");
        }
        else
        {
            _ui.SetMessage(error);
        }

        RefreshBuildUi();
    }

    private void OnShakeDetected(float strength)
    {
        if (!_gameStarted)
        {
            return;
        }

        Input.VibrateHandheld(70);
        (PcPart? popped, string message) = _build.HandleShake(strength);
        if (popped != null)
        {
            _state.AddPart(popped);
        }

        if (_heat.IsRunning)
        {
            _heat.AddHeatShock();
            message += " А ещё от тряски температура подскочила. Физика беспощадна.";
        }

        _ui.SetMessage(message);
        RefreshBuildUi();
    }

    private void RefreshBuildUi()
    {
        _ui.SetBuildSummary(_build.GetSummary());
        _ui.SetSelectedText(_build.DescribeSelected());
        bool canHammer = _build.Selected != null
            && _build.Installed.TryGetValue(_build.Selected.Value, out BuildController.InstalledPart entry)
            && entry.NeedsHammer;
        _ui.SetHammerEnabled(canHammer);
        _ui.SetPasteState(_build.CpuPaste01, _pasteMode);
        if (_build.CpuHasPaste && _pasteMode)
        {
            _pasteMode = false;
            _ui.SetMessage("Майонез нанесён! Процессор блестит, как салат. Так держать — в прямом смысле слова «держать».");
            _ui.SetPasteState(_build.CpuPaste01, false);
        }
    }

    private void ShowFoundPart(PcPart part, Vector3 position)
    {
        Node3D visual = PartVisualFactory.Create(part);
        visual.Position = position;
        visual.Scale = Vector3.One * 0.9f;
        AddChild(visual);

        Tween tween = CreateTween();
        tween.TweenProperty(visual, "position", position + new Vector3(0, 0.85f, 0), 0.5)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(1.6);
        tween.TweenCallback(Callable.From(() => visual.QueueFree()));
    }

    // ---------- Указатель: три режима ----------

    private void BeginPointer(Vector2 position)
    {
        _pointerDown = true;
        _dragging = false;
        _pressPosition = position;
        if (_build.IsPlacing)
        {
            _build.UpdatePlacement(position, _cameraRig.Camera);
        }
    }

    private void MovePointer(Vector2 position, Vector2 relative)
    {
        if (_build.IsPlacing)
        {
            _dragging = true;
            _build.UpdatePlacement(position, _cameraRig.Camera);
            return;
        }

        if (_pasteMode)
        {
            _dragging = true;
            if (_build.ApplyPasteSwipe(position, _cameraRig.Camera, relative.Length()))
            {
                _ui.SetPasteState(_build.CpuPaste01, true);
                if (_build.CpuHasPaste)
                {
                    _pasteMode = false;
                    _ui.SetPasteState(_build.CpuPaste01, false);
                    _ui.SetMessage("Майонез нанесён как надо (70%+). Теперь кулер сверху — и можно в тест, храбрец.");
                    RefreshBuildUi();
                }
            }

            return;
        }

        if (!_dragging && position.DistanceTo(_pressPosition) > DragThresholdPx)
        {
            _dragging = true;
        }

        if (_dragging)
        {
            _cameraRig.RotateYaw(-relative.X * 0.008f);
        }
    }

    private void EndPointer(Vector2 position)
    {
        if (!_pointerDown)
        {
            return;
        }

        _pointerDown = false;
        _dragging = false;

        if (_build.IsPlacing)
        {
            (bool placed, string message) = _build.CompletePlacement(position, _cameraRig.Camera);
            if (placed)
            {
                PcPart? placedPart = _build.Selected != null
                    && _build.Installed.TryGetValue(_build.Selected.Value, out BuildController.InstalledPart entry)
                    ? entry.Part : null;
                if (placedPart != null)
                {
                    _state.RemovePart(placedPart);
                }
            }

            _ui.SetMessage(message);
            RefreshBuildUi();
            return;
        }

        if (_pasteMode)
        {
            return;
        }

        if (position.DistanceTo(_pressPosition) > DragThresholdPx)
        {
            return;
        }

        ulong now = Time.GetTicksMsec();
        if (now - _lastTapMsec < 90)
        {
            return;
        }

        _lastTapMsec = now;
        HandleTap(position);
    }

    private void HandleTap(Vector2 screenPosition)
    {
        Camera3D camera = _cameraRig.Camera;
        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 end = origin + camera.ProjectRayNormal(screenPosition) * 120.0f;

        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
        {
            return;
        }

        var collider = hit["collider"].AsGodotObject() as Node;
        if (collider == null)
        {
            return;
        }

        if (collider.IsInGroup(TrashHeap.GroupName))
        {
            DigAtHeap();
        }
        else if (collider.IsInGroup(DeliveryBox.GroupName))
        {
            OpenDeliveryBox();
        }
        else if (collider.IsInGroup(BuildController.PartGroupName))
        {
            if (_build.TrySelectByCollider(collider, out string description))
            {
                _ui.SetMessage($"Выбрано: {description}");
                RefreshBuildUi();
            }
        }
    }

    private void BuildEnvironment()
    {
        var environment = new Godot.Environment
        {
            BackgroundColor = new Color(0.07f, 0.06f, 0.05f),
            AmbientLightColor = new Color(0.55f, 0.48f, 0.38f),
            AmbientLightEnergy = 0.55f,
            GlowEnabled = false,
            FogEnabled = false
        };
        // Режимы фона и рассеянного света: 1 = Color у background_mode, 2 = Color у ambient_light_source.
        // Через Set(), чтобы не зависеть от имён вложенных enum в биндингах GodotSharp.
        environment.Set("background_mode", 1);
        environment.Set("ambient_light_source", 2);
        AddChild(new WorldEnvironment { Environment = environment });
    }
}
