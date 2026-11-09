using System;
using Godot;

namespace TrashPcTycoon.UI;

/// <summary>
/// Весь 2D-интерфейс, собранный в коде: главное меню, HUD, инвентарь, магазин
/// AliExpress и (Модуль 2) панель сборки с молотом, майонезом, продувкой и
/// термометром стресс-теста. Цвета — картон, ржавчина и грязный металл.
/// </summary>
public partial class MainUI : CanvasLayer
{
    public event Action? PlayPressed;
    public event Action? DigPressed;
    public event Action? BuyBoxPressed;
    public event Action? OpenBoxPressed;
    public event Action<PcPart>? SellPartPressed;
    public event Action<PcPart>? InstallPartPressed;
    public event Action? HammerPressed;
    public event Action? RemoveSelectedPressed;
    public event Action? PasteModePressed;
    public event Action? BlowButtonDown;
    public event Action? BlowButtonUp;
    public event Action? StartTestPressed;
    public event Action? StopTestPressed;
    public event Action? SavePressed;
    public event Action<string>? BuyToolPressed;
    public event Action? ClientNextPressed;
    public event Action? AcceptOrderPressed;
    public event Action? LaunchCyberpunkPressed;
    public event Action? SellPcPressed;
    public event Action? RejectClientPressed;
    public event Action? OpenCasePressed;
    public event Action? SummonClientPressed;
    public event Action<int>? BuyGaragePressed;
    /// <summary>2.0: поехать на отдельную локацию «Свалка».</summary>
    public event Action? GoToDumpPressed;
    /// <summary>2.0: результат мини-игры майонеза (покрытие %, потёки %).</summary>
    public event Action<float, float>? MayoApplied;
    /// <summary>2.0: тап «Охладить» в интерактивном тесте Киберпанка.</summary>
    public event Action? CoolTapPressed;

    private GameState _state = null!;

    private Control _hud = null!;
    private Control _menuOverlay = null!;
    private Control _helpPanel = null!;
    private Control _inventoryPanel = null!;
    private Control _shopPanel = null!;
    private Control _buildPanel = null!;
    private Control _clientPanel = null!;
    private Label _clientNameLabel = null!;
    private Label _clientTextLabel = null!;
    private Label _clientStatusLabel = null!;
    private Button _clientNextButton = null!;
    private Button _acceptOrderButton = null!;
    private Button _launchButton = null!;
    private Button _sellPcButton = null!;
    private Button _rejectButton = null!;
    private VBoxContainer _toolsList = null!;
    private VBoxContainer _garageList = null!;
    private Label _reputationLabel = null!;
    private Label _moneyLabel = null!;
    private Label _inventoryCountLabel = null!;
    private Label _heatLabel = null!;
    private Label _messageLabel = null!;
    private Label _shopStatusLabel = null!;
    private Label _shopMoneyLabel = null!;
    private Label _buildSummaryLabel = null!;
    private Label _selectedLabel = null!;
    private Label _sensorLabel = null!;
    private Label _tempLabel = null!;
    private Label _pasteLabel = null!;
    private VBoxContainer _inventoryList = null!;
    private Button _buyButton = null!;
    private Button _openButton = null!;
    private Button _hammerButton = null!;
    private Button _pasteButton = null!;
    private Button _startTestButton = null!;
    private Button _stopTestButton = null!;
    private ProgressBar _tempBar = null!;
    private ProgressBar _micBar = null!;
    private Control _testOverlay = null!;
    private ProgressBar _testTempBar = null!;
    private Label _testTempLabel = null!;
    private Label _testRedZoneLabel = null!;
    private Label _testTimeLabel = null!;
    private MayoMinigame _mayoMinigame = null!;

    private static readonly Color PanelColor = new(0.14f, 0.115f, 0.09f, 0.96f);
    private static readonly Color ButtonColor = new(0.38f, 0.27f, 0.16f, 1f);
    private static readonly Color ButtonHoverColor = new(0.48f, 0.35f, 0.21f, 1f);
    private static readonly Color TextColor = new(0.90f, 0.85f, 0.72f, 1f);
    private static readonly Color AccentColor = new(0.82f, 0.64f, 0.34f, 1f);

    public override void _Ready()
    {
        BuildHud();
        BuildMenu();
        BuildHelp();
        BuildInventory();
        BuildShop();
        BuildBuildPanel();
        BuildClientPanel();
        BuildMessageBar();
        BuildTestOverlay();

        _mayoMinigame = new MayoMinigame();
        _mayoMinigame.Finished += (coverage, leak) => MayoApplied?.Invoke(coverage, leak);
        _mayoMinigame.Cancelled += () => SetMessage("Майонез отложен. Процессор ждёт свой бутерброд.");
        AddChild(_mayoMinigame);

        _hud.Visible = false;
    }

    /// <summary>Открывает 2D-мини-игру майонеза поверх всего (Обновление 2.0).</summary>
    public void OpenMayoOverlay(float currentPaste01)
    {
        _mayoMinigame.Open(currentPaste01);
    }

    /// <summary>Интерактивный тест 2.0: ползунок температуры вместо чистого таймера.</summary>
    public void SetTestOverlayVisible(bool visible) => _testOverlay.Visible = visible;

    public void SetTestOverlay(float celsius, float secondsLeft, bool running, float redZoneSeconds, bool isChallenge)
    {
        if (!running)
        {
            _testOverlay.Visible = false;
            return;
        }

        _testOverlay.Visible = true;
        _testTempBar.Value = celsius;
        Color fill = celsius >= HeatSystem.CriticalCelsius
            ? new Color(0.78f, 0.24f, 0.15f)
            : celsius >= HeatSystem.WarningCelsius
                ? new Color(0.82f, 0.52f, 0.20f)
                : new Color(0.55f, 0.48f, 0.30f);
        _testTempBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = fill, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 });
        _testTempLabel.Text = $"🌡 {celsius:0} °C — тапай «Охладить», не дай стрелке жить в красной зоне!";
        _testRedZoneLabel.Text = redZoneSeconds > 0.05f
            ? $"КРАСНАЯ ЗОНА: {redZoneSeconds:0.0} / 3.0 сек — ВЗРЫВ БЛИЗКО"
            : "";
        _testTimeLabel.Text = isChallenge
            ? $"Киберпанк при клиенте: осталось {Mathf.CeilToInt(secondsLeft)} сек"
            : $"Тест: осталось {Mathf.CeilToInt(secondsLeft)} сек";
    }

    private void BuildTestOverlay()
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        panel.Position = new Vector2(-330, 74);
        panel.CustomMinimumSize = new Vector2(660, 0);
        panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
        panel.Visible = false;
        AddChild(panel);
        _testOverlay = panel;

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        _testTimeLabel = MakeLabel("Тест: 30 сек", 22);
        content.AddChild(_testTimeLabel);

        _testTempBar = new ProgressBar { MinValue = 30, MaxValue = 110, Value = HeatSystem.AmbientCelsius, ShowPercentage = false, CustomMinimumSize = new Vector2(620, 26) };
        content.AddChild(_testTempBar);

        _testTempLabel = MakeLabel("🌡 34 °C — тапай «Охладить»!", 22);
        content.AddChild(_testTempLabel);

        _testRedZoneLabel = MakeLabel("", 21);
        _testRedZoneLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.45f, 0.35f));
        content.AddChild(_testRedZoneLabel);

        Button cool = MakeButton("ОХЛАДИТЬ (тап по вентилятору) ❄");
        cool.CustomMinimumSize = new Vector2(0, 58);
        cool.AddThemeFontSizeOverride("font_size", 24);
        cool.Pressed += () => CoolTapPressed?.Invoke();
        content.AddChild(cool);
    }

    public void Bind(GameState state)
    {
        _state = state;
        _state.MoneyChanged += _ => RefreshMoney();
        _state.InventoryChanged += _ => RefreshInventory();
        _state.ReputationChanged += _ => RefreshReputation();
        _state.GarageLevelChanged += _ => { RefreshReputation(); RefreshGarageUpgrades(); };
        RefreshMoney();
        RefreshInventory();
        RefreshReputation();
    }

    private void RefreshReputation()
    {
        _reputationLabel.Text = $"Репутация: {_state.StarRating:0.0}/5 ({_state.Reputation}) • Гараж ур. {_state.GarageLevel}";
    }

    public void RefreshGarageUpgrades()
    {
        foreach (Node child in _garageList.GetChildren())
        {
            child.QueueFree();
        }

        foreach (GarageCatalog.LevelDef def in GarageCatalog.Levels)
        {
            bool owned = _state.GarageLevel >= def.Level;
            bool isNext = _state.GarageLevel + 1 == def.Level;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            Label label = MakeLabel(owned
                ? $"Уровень {def.Level}: {def.Name} — ТВОЙ\n{def.Description}"
                : $"Уровень {def.Level}: {def.Name} — {def.PriceRub} ₽\n{def.Description}", 18);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(label);

            if (isNext)
            {
                Button buy = MakeButton("Улучшить");
                buy.Disabled = !_state.CanAfford(def.PriceRub);
                int level = def.Level;
                buy.Pressed += () => BuyGaragePressed?.Invoke(level);
                row.AddChild(buy);
            }

            _garageList.AddChild(row);
            _garageList.AddChild(new HSeparator());
        }
    }

    public void ShowGame()
    {
        _menuOverlay.Visible = false;
        _helpPanel.Visible = false;
        _hud.Visible = true;
    }

    public void ShowMenu()
    {
        _hud.Visible = false;
        _inventoryPanel.Visible = false;
        _shopPanel.Visible = false;
        _buildPanel.Visible = false;
        _menuOverlay.Visible = true;
    }

    public void OpenBuildPanel()
    {
        _inventoryPanel.Visible = false;
        _shopPanel.Visible = false;
        _helpPanel.Visible = false;
        _buildPanel.Visible = true;
    }

    public bool IsBuildPanelOpen => _buildPanel.Visible;

    public void ClosePanels()
    {
        _inventoryPanel.Visible = false;
        _shopPanel.Visible = false;
        _buildPanel.Visible = false;
        _clientPanel.Visible = false;
        _helpPanel.Visible = false;
    }

    public void OpenClientPanel()
    {
        ClosePanels();
        _clientPanel.Visible = true;
    }

    public bool IsClientPanelOpen => _clientPanel.Visible;

    public void SetClientDialogue(string name, string text, string status,
        bool canNext, bool canAccept, bool canLaunch, bool canSell, bool canReject)
    {
        _clientNameLabel.Text = name;
        _clientTextLabel.Text = text;
        _clientStatusLabel.Text = status;
        _clientNextButton.Disabled = !canNext;
        _acceptOrderButton.Disabled = !canAccept;
        _launchButton.Disabled = !canLaunch;
        _sellPcButton.Disabled = !canSell;
        _rejectButton.Disabled = !canReject;
    }

    public void RefreshTools()
    {
        foreach (Node child in _toolsList.GetChildren())
        {
            child.QueueFree();
        }

        foreach (ToolsCatalog.ToolDef def in ToolsCatalog.All)
        {
            bool owned = _state.OwnsTool(def.Id);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            Label label = MakeLabel(owned
                ? $"{def.Name} — КУПЛЕН\n{def.Description}"
                : $"{def.Name} — {def.PriceRub} ₽\n{def.Description}", 18);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(label);

            if (!owned)
            {
                Button buy = MakeButton("Купить");
                buy.Disabled = !_state.CanAfford(def.PriceRub);
                string id = def.Id;
                buy.Pressed += () => BuyToolPressed?.Invoke(id);
                row.AddChild(buy);
            }

            _toolsList.AddChild(row);
            _toolsList.AddChild(new HSeparator());
        }
    }

    public void SetMessage(string text)
    {
        _messageLabel.Text = text;
    }

    public void SetShopStatus(string text)
    {
        _shopStatusLabel.Text = text;
    }

    public void SetBuyEnabled(bool enabled)
    {
        _buyButton.Disabled = !enabled;
    }

    public void SetOpenEnabled(bool enabled)
    {
        _openButton.Disabled = !enabled;
    }

    public void SetBuildSummary(string text)
    {
        _buildSummaryLabel.Text = text;
    }

    public void SetSelectedText(string text)
    {
        _selectedLabel.Text = $"Выбрано: {text}";
    }

    public void SetHammerEnabled(bool enabled)
    {
        _hammerButton.Disabled = !enabled;
    }

    public void SetPasteState(float paste01, bool active)
    {
        _pasteLabel.Text = active
            ? $"РЕЖИМ МАЙОНЕЗА: води пальцем по процессору… нанесено {Mathf.RoundToInt(paste01 * 100)}%"
            : $"Майонез на процессоре: {Mathf.RoundToInt(paste01 * 100)}% (нужно 70%+)";
        _pasteButton.Text = active ? "Майонез: ВКЛ (води по CPU!)" : "Намазать майонез";
    }

    public void SetSensorStatus(string text)
    {
        _sensorLabel.Text = text;
    }

    public void SetMicLevel(float level, bool blowing)
    {
        _micBar.Value = level * 100.0;
        if (blowing)
        {
            _sensorLabel.Text = "ДУЕШЬ! Пыль летит, ПК остывает. Продолжай!";
        }
    }

    public void SetTemperature(float celsius, float secondsLeft, bool running)
    {
        _tempBar.Value = celsius;
        Color fill = celsius >= HeatSystem.CriticalCelsius
            ? new Color(0.72f, 0.22f, 0.14f)
            : celsius >= HeatSystem.WarningCelsius
                ? new Color(0.78f, 0.50f, 0.20f)
                : new Color(0.55f, 0.48f, 0.30f);
        var style = new StyleBoxFlat { BgColor = fill, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
        _tempBar.AddThemeStyleboxOverride("fill", style);

        _tempLabel.Text = running
            ? $"Температура: {celsius:0}°C • до конца теста: {Mathf.CeilToInt(secondsLeft)} сек"
            : $"Температура: {celsius:0}°C • тест не идёт";
        _heatLabel.Text = $"ПК: {celsius:0}°C";
        _startTestButton.Disabled = running;
        _stopTestButton.Disabled = !running;
    }

    private void RefreshMoney()
    {
        _moneyLabel.Text = $"Деньги: {_state.Money} ₽";
        _shopMoneyLabel.Text = $"В кассе: {_state.Money} ₽   •   Коробка: {PartCatalog.AliBoxPriceRub} ₽";
        _buyButton.Disabled = !_state.CanAfford(PartCatalog.AliBoxPriceRub);
        RefreshTools();
        RefreshGarageUpgrades();
    }

    private void RefreshInventory()
    {
        _inventoryCountLabel.Text = $"Деталей: {_state.Inventory.Count}";
        foreach (Node child in _inventoryList.GetChildren())
        {
            child.QueueFree();
        }

        if (_state.Inventory.Count == 0)
        {
            _inventoryList.AddChild(MakeLabel("Пока пусто. Свалка сама себя не разберёт.", 20));
            return;
        }

        foreach (PcPart part in _state.Inventory)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);

            Label label = MakeLabel($"{part.InventoryLine}\n{part.Description}", 19);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(label);

            PcPart captured = part;
            var install = MakeButton("На стол");
            install.Pressed += () => InstallPartPressed?.Invoke(captured);
            row.AddChild(install);

            var sell = MakeButton($"Сдать за {part.ValueRub} ₽");
            sell.Pressed += () => SellPartPressed?.Invoke(captured);
            row.AddChild(sell);
            _inventoryList.AddChild(row);
            _inventoryList.AddChild(new HSeparator());
        }
    }

    private void BuildHud()
    {
        _hud = new Control { Name = "Hud" };
        _hud.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _hud.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_hud);

        var topBar = new PanelContainer();
        topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        topBar.AddThemeStyleboxOverride("panel", MakePanelStyle());
        _hud.AddChild(topBar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        topBar.AddChild(row);

        _moneyLabel = MakeLabel("Деньги: 0 ₽", 26);
        row.AddChild(_moneyLabel);
        _inventoryCountLabel = MakeLabel("Деталей: 0", 26);
        row.AddChild(_inventoryCountLabel);
        _heatLabel = MakeLabel("ПК: 34°C", 26);
        row.AddChild(_heatLabel);
        _reputationLabel = MakeLabel("Репутация: 2.0/5 (40) • Гараж ур. 1", 22);
        row.AddChild(_reputationLabel);

        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(spacer);

        Button build = MakeButton("СБОРКА");
        build.Pressed += OpenBuildPanel;
        row.AddChild(build);

        Button client = MakeButton("Клиент");
        client.Pressed += () =>
        {
            SummonClientPressed?.Invoke();
            OpenClientPanel();
        };
        row.AddChild(client);

        Button save = MakeButton("Сохранить");
        save.Pressed += () => SavePressed?.Invoke();
        row.AddChild(save);

        Button shop = MakeButton("Магазин AliExpress");
        shop.Pressed += () => TogglePanel(_shopPanel);
        row.AddChild(shop);

        Button dump = MakeButton("Поехать на свалку");
        dump.Pressed += () => GoToDumpPressed?.Invoke();
        row.AddChild(dump);

        Button inventory = MakeButton("Инвентарь");
        inventory.Pressed += () => TogglePanel(_inventoryPanel);
        row.AddChild(inventory);

        Button help = MakeButton("Как играть");
        help.Pressed += () => _helpPanel.Visible = true;
        row.AddChild(help);

        Button menu = MakeButton("Меню");
        menu.Pressed += ShowMenu;
        row.AddChild(menu);

        var digButton = MakeButton("КОПАТЬ НА СВАЛКЕ");
        digButton.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        digButton.Position = new Vector2(-330, -110);
        digButton.Size = new Vector2(310, 78);
        digButton.AddThemeFontSizeOverride("font_size", 26);
        digButton.Pressed += () => DigPressed?.Invoke();
        _hud.AddChild(digButton);

        var hint = MakeLabel("Тап по куче — копать • Тяни — камера / деталь / майонез • Тряхни телефон — проверка на «отвалилось»", 18);
        hint.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        hint.Position = new Vector2(18, -42);
        _hud.AddChild(hint);
    }

    private void BuildMessageBar()
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        panel.OffsetTop = -92;
        panel.OffsetBottom = -48;
        panel.OffsetLeft = 16;
        panel.OffsetRight = -350;
        panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
        _hud.AddChild(panel);

        _messageLabel = MakeLabel("Добро пожаловать в гараж.", 21);
        _messageLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        panel.AddChild(_messageLabel);
    }

    private void BuildMenu()
    {
        _menuOverlay = new PanelContainer();
        _menuOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _menuOverlay.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.07f, 0.06f, 0.05f, 0.88f) });
        AddChild(_menuOverlay);

        var center = new CenterContainer();
        _menuOverlay.AddChild(center);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        center.AddChild(box);

        Label title = MakeLabel("TRASH-PC TYCOON 3D", 54);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", AccentColor);
        box.AddChild(title);

        Label subtitle = MakeLabel("Собери компьютер из мусора.\nПродай, пока он не задымился.", 26);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(subtitle);

        Button play = MakeButton("ИГРАТЬ");
        play.CustomMinimumSize = new Vector2(320, 64);
        play.AddThemeFontSizeOverride("font_size", 30);
        play.Pressed += () => PlayPressed?.Invoke();
        box.AddChild(play);

        Button help = MakeButton("Как играть");
        help.CustomMinimumSize = new Vector2(320, 52);
        help.Pressed += () => _helpPanel.Visible = true;
        box.AddChild(help);

        Button quit = MakeButton("Выйти");
        quit.CustomMinimumSize = new Vector2(320, 52);
        quit.Pressed += () => GetTree().Quit();
        box.AddChild(quit);

        Label version = MakeLabel("Модуль 2: сборка, молоток, майонез и перегрев", 18);
        version.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(version);
    }

    private void BuildHelp()
    {
        _helpPanel = MakeSidePanel("Как играть");
        VBoxContainer content = GetPanelContent(_helpPanel);
        content.AddChild(MakeWrapped(
            "СВАЛКА И МАГАЗИН (Модуль 1)\n" +
            "Тапай по куче мусора — выкапывай детали. В магазине заказывай коробку AliExpress за 150 ₽ и вскрывай тапом по коробке на верстаке.\n\n" +
            "СБОРКА (Модуль 2)\n" +
            "1. «Инвентарь» → «На стол» у детали, затем тапни/проведи по столу сборки в центре гаража — деталь встанет туда.\n" +
            "2. Сначала нужен корпус, потом материнка, процессор, кулер, блок питания и память.\n" +
            "3. Не влезла? Тапни по ней и жми «МОЛОТОК». Два удара — и геометрия сдастся (но греться будет сильнее).\n" +
            "4. Майонез: кнопка «Намазать майонез», затем води пальцем по процессору на столе.\n" +
            "5. Продувка: дуй в микрофон телефона (или зажми кнопку «Дуть») — пыль долой, температура ниже.\n" +
            "6. Тряхни телефон — узнаешь, что там гремит. Иногда деталь вылетает. Честная диагностика.\n" +
            "7. «СТАРТ ТЕСТА»: 30 секунд. 88°C — вибрирует и дымит, 100°C — всё, приехали. Дуй, чтобы выжить!\n\n" +
            "КЛИЕНТ И ПРОДАЖА (Модуль 3)\n" +
            "Школьник приходит сам или по кнопке «Клиент». Выслушай его, прими заказ, собери ПК и жми «Запустить Киберпанк»: 60 секунд под нагрузкой при клиенте. Дуй в микрофон, открывай корпус кнопкой в панели сборки (-12°C). Прожил минуту — продавай за цену деталей x1.6 + 250 ₽. Дошёл до 100°C — одна деталь сгорит, клиент уйдёт.\n" +
            "Заработанное трать в магазине на инструменты: молоток «Убедитель», пылесос, настоящую термопасту и комнатный вентилятор. Кнопка «Сохранить» пишет прогресс, игра и сама сохраняется после важных событий.\n\n" +
            "МОДУЛЬ 4\n" +
            "Клиенты разные: бабушке важен тихий ПК (пик до 88°C), стримеры и дяди из бухгалтерии заходят только в приличный гараж. Репутация растёт от хороших продаж и падает от дымящихся провалов, а в магазине гараж улучшается до «Мастерской». И остерегайся кота.", 20));
        AddCloseButton(content, _helpPanel);
        _helpPanel.Visible = false;
    }

    private void BuildInventory()
    {
        _inventoryPanel = MakeSidePanel("Инвентарь");
        VBoxContainer content = GetPanelContent(_inventoryPanel);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(620, 430) };
        _inventoryList = new VBoxContainer();
        _inventoryList.AddThemeConstantOverride("separation", 8);
        _inventoryList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_inventoryList);
        content.AddChild(scroll);
        AddCloseButton(content, _inventoryPanel);
        _inventoryPanel.Visible = false;
    }

    private void BuildShop()
    {
        _shopPanel = MakeSidePanel("AliExpress — «Всё по-честному»");
        VBoxContainer content = GetPanelContent(_shopPanel);
        _shopMoneyLabel = MakeLabel("", 22);
        content.AddChild(_shopMoneyLabel);
        content.AddChild(MakeWrapped(
            "Загадочная коробка с деталью. Что внутри — не знает даже продавец. " +
            "Судя по отзывам, внутри «всё суппер, работает, пять звёзд, но не включается».", 21));

        _buyButton = MakeButton($"Заказать коробку за {PartCatalog.AliBoxPriceRub} ₽");
        _buyButton.CustomMinimumSize = new Vector2(420, 56);
        _buyButton.Pressed += () => BuyBoxPressed?.Invoke();
        content.AddChild(_buyButton);

        _openButton = MakeButton("Вскрыть коробку");
        _openButton.CustomMinimumSize = new Vector2(420, 56);
        _openButton.Disabled = true;
        _openButton.Pressed += () => OpenBoxPressed?.Invoke();
        content.AddChild(_openButton);

        _shopStatusLabel = MakeWrapped("Коробок в пути нет. Гараж пуст и готов к разочарованиям.", 20);
        content.AddChild(_shopStatusLabel);
        Label toolsTitle = MakeLabel("Инструменты (навсегда твои)", 24);
        toolsTitle.AddThemeColorOverride("font_color", AccentColor);
        content.AddChild(toolsTitle);

        var toolsScroll = new ScrollContainer { CustomMinimumSize = new Vector2(620, 150) };
        _toolsList = new VBoxContainer();
        _toolsList.AddThemeConstantOverride("separation", 8);
        _toolsList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        toolsScroll.AddChild(_toolsList);
        content.AddChild(toolsScroll);

        Label garageTitle = MakeLabel("Улучшение гаража", 24);
        garageTitle.AddThemeColorOverride("font_color", AccentColor);
        content.AddChild(garageTitle);

        var garageScroll = new ScrollContainer { CustomMinimumSize = new Vector2(620, 150) };
        _garageList = new VBoxContainer();
        _garageList.AddThemeConstantOverride("separation", 8);
        _garageList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        garageScroll.AddChild(_garageList);
        content.AddChild(garageScroll);

        AddCloseButton(content, _shopPanel);
        _shopPanel.Visible = false;
    }

    private void BuildClientPanel()
    {
        _clientPanel = MakeSidePanel("Клиент");
        VBoxContainer content = GetPanelContent(_clientPanel);

        _clientNameLabel = MakeLabel("Клиента нет", 26);
        _clientNameLabel.AddThemeColorOverride("font_color", AccentColor);
        content.AddChild(_clientNameLabel);

        _clientTextLabel = MakeWrapped("У прилавка пусто. Нажми «Клиент» вверху, чтобы позвать следующего мечтателя, или подожди — они сами находят гараж по запаху палёной проводки.", 21);
        content.AddChild(_clientTextLabel);

        _clientStatusLabel = MakeWrapped("", 19);
        content.AddChild(_clientStatusLabel);

        _clientNextButton = MakeButton("Далее");
        _clientNextButton.Pressed += () => ClientNextPressed?.Invoke();
        content.AddChild(_clientNextButton);

        _acceptOrderButton = MakeButton("Принять заказ");
        _acceptOrderButton.Pressed += () => AcceptOrderPressed?.Invoke();
        content.AddChild(_acceptOrderButton);

        _launchButton = MakeButton("Запустить Киберпанк (60 секунд выживания!)");
        _launchButton.Pressed += () => LaunchCyberpunkPressed?.Invoke();
        content.AddChild(_launchButton);

        _sellPcButton = MakeButton("Продать ПК");
        _sellPcButton.Pressed += () => SellPcPressed?.Invoke();
        content.AddChild(_sellPcButton);

        _rejectButton = MakeButton("Отказать (уходи, мечтатель)");
        _rejectButton.Pressed += () => RejectClientPressed?.Invoke();
        content.AddChild(_rejectButton);

        AddCloseButton(content, _clientPanel);
        _clientPanel.Visible = false;
    }

    private void BuildBuildPanel()
    {
        _buildPanel = MakeSidePanel("Стол сборки");
        VBoxContainer content = GetPanelContent(_buildPanel);

        _buildSummaryLabel = MakeWrapped("Стол пуст.", 19);
        content.AddChild(_buildSummaryLabel);

        _selectedLabel = MakeLabel("Выбрано: ничего", 20);
        content.AddChild(_selectedLabel);

        var toolRow = new HBoxContainer();
        toolRow.AddThemeConstantOverride("separation", 10);
        content.AddChild(toolRow);

        _hammerButton = MakeButton("🔨 МОЛОТОК");
        _hammerButton.Pressed += () => HammerPressed?.Invoke();
        toolRow.AddChild(_hammerButton);

        Button remove = MakeButton("Снять деталь");
        remove.Pressed += () => RemoveSelectedPressed?.Invoke();
        toolRow.AddChild(remove);

        var blow = MakeButton("💨 Дуть (зажми)");
        blow.ButtonDown += () => BlowButtonDown?.Invoke();
        blow.ButtonUp += () => BlowButtonUp?.Invoke();
        toolRow.AddChild(blow);

        _pasteButton = MakeButton("Намазать майонез (мини-игра)");
        _pasteButton.Pressed += () => PasteModePressed?.Invoke();
        content.AddChild(_pasteButton);

        _pasteLabel = MakeLabel("Майонез на процессоре: 0% (нужно 70%+)", 19);
        content.AddChild(_pasteLabel);

        _sensorLabel = MakeWrapped("Датчики ещё спят.", 18);
        content.AddChild(_sensorLabel);

        _micBar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 0, CustomMinimumSize = new Vector2(620, 18), ShowPercentage = false };
        content.AddChild(_micBar);

        _tempBar = new ProgressBar { MinValue = 0, MaxValue = 110, Value = HeatSystem.AmbientCelsius, CustomMinimumSize = new Vector2(620, 26), ShowPercentage = false };
        content.AddChild(_tempBar);

        _tempLabel = MakeLabel("Температура: 34°C • тест не идёт", 21);
        content.AddChild(_tempLabel);

        var testRow = new HBoxContainer();
        testRow.AddThemeConstantOverride("separation", 10);
        content.AddChild(testRow);

        _startTestButton = MakeButton("СТАРТ ТЕСТА (30 сек)");
        _startTestButton.Pressed += () => StartTestPressed?.Invoke();
        testRow.AddChild(_startTestButton);

        _stopTestButton = MakeButton("■ Стоп");
        _stopTestButton.Disabled = true;
        _stopTestButton.Pressed += () => StopTestPressed?.Invoke();
        testRow.AddChild(_stopTestButton);

        Button openCase = MakeButton("Открыть корпус (-12°C, раз в 20 сек)");
        openCase.Pressed += () => OpenCasePressed?.Invoke();
        content.AddChild(openCase);

        AddCloseButton(content, _buildPanel);
        _buildPanel.Visible = false;
    }

    private Control MakeSidePanel(string title)
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(680, 560);
        panel.Position = new Vector2(-340, -280);
        panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
        AddChild(panel);

        var content = new VBoxContainer { Name = "Content" };
        content.AddThemeConstantOverride("separation", 12);
        panel.AddChild(content);

        Label titleLabel = MakeLabel(title, 30);
        titleLabel.AddThemeColorOverride("font_color", AccentColor);
        content.AddChild(titleLabel);
        return panel;
    }

    private static VBoxContainer GetPanelContent(Control panel) => panel.GetNode<VBoxContainer>("Content");

    private void AddCloseButton(VBoxContainer content, Control panel)
    {
        Button close = MakeButton("Закрыть");
        close.Pressed += () => panel.Visible = false;
        content.AddChild(close);
    }

    private void TogglePanel(Control panel)
    {
        bool show = !panel.Visible;
        _inventoryPanel.Visible = false;
        _shopPanel.Visible = false;
        _buildPanel.Visible = false;
        _clientPanel.Visible = false;
        _helpPanel.Visible = false;
        panel.Visible = show;
        if (show && panel == _inventoryPanel)
        {
            RefreshInventory();
        }
    }

    private static StyleBoxFlat MakePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = PanelColor,
            BorderColor = new Color(0.45f, 0.34f, 0.20f),
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthTop = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 10,
            ContentMarginBottom = 10
        };
    }

    private static Button MakeButton(string text)
    {
        var button = new Button { Text = text };
        button.AddThemeColorOverride("font_color", TextColor);
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.95f, 0.82f));
        button.AddThemeFontSizeOverride("font_size", 21);
        var normal = new StyleBoxFlat
        {
            BgColor = ButtonColor,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = ButtonHoverColor;
        var disabled = (StyleBoxFlat)normal.Duplicate();
        disabled.BgColor = new Color(0.25f, 0.22f, 0.19f);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeStyleboxOverride("disabled", disabled);
        return button;
    }

    private static Label MakeLabel(string text, int fontSize)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    private static Label MakeWrapped(string text, int fontSize)
    {
        Label label = MakeLabel(text, fontSize);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(620, 0);
        return label;
    }
}
