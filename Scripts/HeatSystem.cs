using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Стресс-тест собранного ПК: таймер, рост температуры по состоянию сборки
/// (пыль, фейки, вбитые молотком детали, майонез вместо термопасты),
/// продувка как охлаждение, вибрация на критической температуре и дым при провале.
/// Выживание клиента на 60 секунд — уже Модуль 3, здесь честные 30 секунд теста.
/// </summary>
public partial class HeatSystem : Node
{
    [Signal] public delegate void TemperatureChangedEventHandler(float celsius, float secondsLeft, bool running);
    [Signal] public delegate void CriticalTemperatureEventHandler(float celsius);
    [Signal] public delegate void TestFinishedEventHandler(bool survived, string summary);

    public const float AmbientCelsius = 34.0f;
    public const float WarningCelsius = 75.0f;
    public const float CriticalCelsius = 88.0f;
    public const float FailCelsius = 100.0f;
    public const float TestDurationSeconds = 30.0f;

    private BuildController? _build;
    private bool _coolingActive;
    private bool _criticalAnnounced;
    private float _vibrateTimer;

    public float Temperature { get; private set; } = AmbientCelsius;
    public float SecondsLeft { get; private set; }
    public bool IsRunning { get; private set; }

    /// <summary>Режим «Киберпанк при клиенте»: 60 секунд под повышенной нагрузкой (Модуль 3).</summary>
    public bool InChallenge { get; private set; }
    public const float ChallengeDurationSeconds = 60.0f;
    public const float ChallengeLoadMultiplier = 1.35f;

    /// <summary>Комнатный вентилятор из магазина инструментов: дует всегда, не устаёт.</summary>
    public float PassiveCoolingPerSecond { get; set; }

    public void Bind(BuildController build)
    {
        _build = build;
    }

    public void SetExternalCooling(bool active)
    {
        _coolingActive = active;
    }

    public bool StartTest(out string error)
    {
        error = "";
        if (_build == null)
        {
            error = "Стол сборки не найден. Гараж съел его, как и всё остальное.";
            return false;
        }

        var missing = _build.GetMissingEssentials();
        if (missing.Count > 0)
        {
            error = $"Тест не запустится, не хватает: {string.Join(", ", missing)}.";
            return false;
        }

        if (IsRunning)
        {
            error = "Тест уже идёт. Два теста одновременно — это уже научная работа.";
            return false;
        }

        IsRunning = true;
        InChallenge = false;
        _criticalAnnounced = false;
        SecondsLeft = TestDurationSeconds;
        Temperature = Mathf.Max(Temperature, AmbientCelsius);
        _build.SetFanSpinning(true);
        EmitTemperature();
        return true;
    }

    /// <summary>Мини-игра на выживание: Киберпанк должен прожить 60 секунд при клиенте.</summary>
    public bool StartChallenge(out string error)
    {
        error = "";
        if (_build == null)
        {
            error = "Стол сборки не найден. Клиент смотрит на пустой стол. Неудобно вышло.";
            return false;
        }

        var missing = _build.GetMissingEssentials();
        if (missing.Count > 0)
        {
            error = $"Клиенту такое не запустишь, не хватает: {string.Join(", ", missing)}.";
            return false;
        }

        if (IsRunning)
        {
            error = "Уже что-то тестируется. ПК один, а амбиций — на серверную.";
            return false;
        }

        IsRunning = true;
        InChallenge = true;
        _criticalAnnounced = false;
        SecondsLeft = ChallengeDurationSeconds;
        Temperature = Mathf.Max(Temperature, AmbientCelsius);
        _build.SetFanSpinning(true);
        EmitTemperature();
        return true;
    }

    /// <summary>Аварийное охлаждение: открыли корпус — минус градусы сразу.</summary>
    public void ApplyInstantCooling(float celsius)
    {
        if (!IsRunning)
        {
            return;
        }

        Temperature = Mathf.Max(AmbientCelsius, Temperature - celsius);
        EmitTemperature();
    }

    public void StopTest(string reason)
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        InChallenge = false;
        _build?.SetFanSpinning(false);
        _build?.SetSmoke(false);
        EmitSignal(SignalName.TestFinished, false, reason);
        EmitTemperature();
    }

    /// <summary>Резкий трясочный удар по температуре во время теста.</summary>
    public void AddHeatShock()
    {
        if (!IsRunning)
        {
            return;
        }

        Temperature = Mathf.Min(FailCelsius, Temperature + 3.0f);
        EmitTemperature();
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;

        if (IsRunning && _build != null)
        {
            SecondsLeft -= d;
            float rate = _build.GetHeatRatePerSecond() * (InChallenge ? ChallengeLoadMultiplier : 1.0f);
            float cooling = (_coolingActive ? 6.5f : 0f) + PassiveCoolingPerSecond;
            Temperature += (rate - cooling) * d;

            bool hddRumble = _build.Installed.TryGetValue(PartType.Hdd, out BuildController.InstalledPart hdd)
                && hdd.Part.Quality == PartQuality.Trash;
            if (hddRumble && Temperature > 55.0f && GD.Randf() < d * 2.0f)
            {
                Input.VibrateHandheld(45);
            }

            _build.SetSmoke(Temperature >= CriticalCelsius);

            if (Temperature >= CriticalCelsius)
            {
                if (!_criticalAnnounced)
                {
                    _criticalAnnounced = true;
                    EmitSignal(SignalName.CriticalTemperature, Temperature);
                }

                _vibrateTimer -= d;
                if (_vibrateTimer <= 0f)
                {
                    _vibrateTimer = 0.45f;
                    Input.VibrateHandheld(180);
                }
            }

            if (Temperature >= FailCelsius)
            {
                Finish(false, InChallenge
                    ? "100°C при клиенте. Дым, запах палёного майонеза, тишина. Одна деталь сгорела насовсем."
                    : "100°C. Что-то внутри сказало «хватит» и перестало быть деталью. ПК не прошёл тест — но как дымил!");
            }
            else if (SecondsLeft <= 0f)
            {
                Finish(true, InChallenge
                    ? $"КИБЕРПАНК ПРОЖИЛ 60 СЕКУНД! Пик — {Temperature:0}°C. Клиент в восторге и уже достаёт копилку."
                    : $"Тест пройден! ПК пережил {TestDurationSeconds:0} секунд и нагрелся «всего» до {Temperature:0}°C. Клиенту такое можно показывать. Издалека.");
            }

            EmitTemperature();
        }
        else if (Temperature > AmbientCelsius)
        {
            Temperature = Mathf.Max(AmbientCelsius, Temperature - 5.0f * d);
            EmitTemperature();
        }
    }

    private void Finish(bool survived, string summary)
    {
        IsRunning = false;
        InChallenge = false;
        _build?.SetFanSpinning(false);
        _build?.SetSmoke(!survived || Temperature >= CriticalCelsius);
        _build?.MarkTestResult(survived);
        if (!survived)
        {
            Input.VibrateHandheld(450);
        }

        GetTree().CreateTimer(2.0).Timeout += () => _build?.SetSmoke(false);

        EmitSignal(SignalName.TestFinished, survived, summary);
        EmitTemperature();
    }

    private void EmitTemperature()
    {
        EmitSignal(SignalName.TemperatureChanged, Temperature, Mathf.Max(0f, SecondsLeft), IsRunning);
    }
}
