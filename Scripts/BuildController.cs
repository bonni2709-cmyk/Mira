using System;
using System.Collections.Generic;
using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Стол сборки и всё, что на нём стоит: открытый корпус, слоты деталей,
/// «не влезло — вобьём молотком», пыль, майонез вместо термопасты.
/// Drag-and-drop сюда ведёт Main: BeginPlacement → UpdatePlacement → CompletePlacement.
/// </summary>
public partial class BuildController : Node3D
{
    public const string PartGroupName = "build_part";

    public sealed class InstalledPart
    {
        public required PcPart Part;
        public required Node3D Visual;
        public Node3D DustVisual = null!;
        public Node3D MayoVisual = null!;
        public bool NeedsHammer;
        public bool Hammered;
        public int HammerHits;
        public float Dust01 = 0.5f;
        public float Paste01;
        public bool TestPassed;
    }

    private readonly Random _rng = new();
    private readonly Dictionary<PartType, InstalledPart> _installed = new();

    private PcPart? _placingPart;
    private Node3D? _placingGhost;
    private PartType? _selected;
    private bool _fanSpinning;
    private bool _smokeActive;
    private float _smokeTimer;

    public bool IsPlacing => _placingPart != null;
    public PartType? Selected => _selected;
    public bool HasCase => _installed.ContainsKey(PartType.ComputerCase);
    public IReadOnlyDictionary<PartType, InstalledPart> Installed => _installed;

    // Эффекты купленных инструментов выставляет Main (см. ToolsCatalog).
    public int HammerHitsRequired { get; set; } = 2;
    public float BlowDustMultiplier { get; set; } = 1.0f;
    public bool HasRealPaste { get; set; }
    /// <summary>2.0/Модуль 2: нож «Швейцарец» отменяет штраф −20% к цене вбитых деталей.</summary>
    public bool KnifeProtectsValue { get; set; }

    /// <summary>2.0/Модуль 2: стоит ли в сборке RGB-лента (её жаждут мажоры).</summary>
    public bool HasRgb => _installed.ContainsKey(PartType.RgbStrip);

    /// <summary>2.0/Модуль 3: в сборке есть HDD — во время теста он будет скрипеть.</summary>
    public bool HasHdd => _installed.ContainsKey(PartType.Hdd);

    /// <summary>2.0/Модуль 3: попал ли последний удар молотка по делу (для звука и тряски).</summary>
    public bool LastHammerLanded { get; private set; }

    private static readonly Dictionary<PartType, Vector3> SlotPositions = new()
    {
        { PartType.ComputerCase, new Vector3(-1.45f, 0f, 0f) },
        { PartType.Motherboard, new Vector3(0.25f, 0.02f, -0.15f) },
        { PartType.Cpu, new Vector3(0.25f, 0.16f, -0.25f) },
        { PartType.Cooler, new Vector3(0.25f, 0.34f, -0.25f) },
        { PartType.Ram, new Vector3(0.72f, 0.14f, 0.28f) },
        { PartType.Gpu, new Vector3(0.10f, 0.22f, 0.52f) },
        { PartType.Psu, new Vector3(-1.45f, 0.10f, 0.45f) },
        { PartType.Hdd, new Vector3(0.95f, 0.10f, -0.55f) },
        { PartType.Ssd, new Vector3(0.95f, 0.08f, 0.05f) },
        // RGB-лента живёт на верхней грани корпуса и светит в душу.
        { PartType.RgbStrip, new Vector3(-1.45f, 1.48f, 0f) },
    };

    private static readonly Vector3 PendingPosition = new(1.65f, 0.05f, 0.45f);
    private const float TableHalfX = 2.5f;
    private const float TableHalfZ = 1.25f;

    public override void _Ready()
    {
        BuildTable();
    }

    public override void _Process(double delta)
    {
        if (_fanSpinning && _installed.TryGetValue(PartType.Cooler, out InstalledPart cooler))
        {
            cooler.Visual.RotateY((float)delta * 14.0f);
        }

        if (_smokeActive)
        {
            _smokeTimer -= (float)delta;
            if (_smokeTimer <= 0f)
            {
                _smokeTimer = 0.12f;
                SpawnSmokePuff();
            }
        }
    }

    // ---------- Drag & Drop ----------

    public string BeginPlacement(PcPart part)
    {
        if (part.Type == PartType.Junk)
        {
            return "Это не деталь, а вещественное доказательство. Сдай барахольщику.";
        }

        if (_installed.ContainsKey(part.Type))
        {
            return $"Слот «{part.TypeName}» уже занят. Сначала сними старую деталь.";
        }

        if (part.Type != PartType.ComputerCase && !HasCase)
        {
            return "Сначала поставь на стол корпус — деталям нужен дом, пусть и картонный район.";
        }

        CancelPlacement();
        _placingPart = part;
        _placingGhost = PartVisualFactory.Create(part);
        SetGhostMaterial(_placingGhost);
        _placingGhost.Position = SlotPositions[part.Type];
        AddChild(_placingGhost);
        return $"Неси «{part.Name}» к столу и отпусти над ним. Мимо стола — деталь передумает.";
    }

    public void UpdatePlacement(Vector2 screenPosition, Camera3D camera)
    {
        if (_placingGhost == null)
        {
            return;
        }

        if (TryScreenToTable(screenPosition, camera, out Vector3 local))
        {
            _placingGhost.Position = new Vector3(local.X, 0.05f, local.Z);
        }
    }

    /// <summary>Возвращает текст результата; placed=true, если деталь ушла из инвентаря на стол.</summary>
    public (bool placed, string message) CompletePlacement(Vector2 screenPosition, Camera3D camera)
    {
        if (_placingPart == null || _placingGhost == null)
        {
            return (false, "");
        }

        PcPart part = _placingPart;
        bool overTable = TryScreenToTable(screenPosition, camera, out Vector3 local)
            && Mathf.Abs(local.X) <= TableHalfX && Mathf.Abs(local.Z) <= TableHalfZ;

        Node3D ghost = _placingGhost;
        _placingPart = null;
        _placingGhost = null;

        if (!overTable)
        {
            ghost.QueueFree();
            return (false, $"«{part.Name}» не долетела до стола и вернулась в инвентарь. Меткость — не твоё.");
        }

        ghost.QueueFree();
        return Install(part);
    }

    public void CancelPlacement()
    {
        _placingGhost?.QueueFree();
        _placingGhost = null;
        _placingPart = null;
    }

    private (bool placed, string message) Install(PcPart part)
    {
        var entry = new InstalledPart
        {
            Part = part,
            Visual = PartVisualFactory.Create(part),
            Dust01 = 0.3f + (float)_rng.NextDouble() * 0.55f
        };

        bool fits = RollFits(part);
        entry.NeedsHammer = !fits;
        entry.Visual.Position = fits ? SlotPositions[part.Type] : PendingPosition;
        if (!fits)
        {
            entry.Visual.RotationDegrees = new Vector3(0, 24f, 7f);
        }

        entry.DustVisual = BuildDustOverlay(entry.Dust01);
        entry.Visual.AddChild(entry.DustVisual);
        if (part.Type == PartType.Cpu)
        {
            entry.MayoVisual = BuildMayoOverlay();
            entry.Visual.AddChild(entry.MayoVisual);
        }

        AddChild(entry.Visual);
        AttachCollider(entry);
        _installed[part.Type] = entry;
        _selected = part.Type;

        if (!fits)
        {
            return (true, $"«{part.Name}» НЕ ВЛЕЗАЕТ. Классика. Выбери её на столе и жми «МОЛОТОК» — вобьём по-гаражному.");
        }

        return (true, $"«{part.Name}» встала в слот «{part.TypeName}». Даже без угроз.");
    }

    private bool RollFits(PcPart part)
    {
        double chance = part.Quality switch
        {
            PartQuality.Working => 0.92,
            PartQuality.Worn => 0.78,
            PartQuality.Trash => 0.58,
            PartQuality.Fake => 0.45,
            _ => 0.7
        };
        return _rng.NextDouble() < chance;
    }

    // ---------- Выбор, снятие, молоток ----------

    public bool TrySelectByCollider(Node collider, out string message)
    {
        message = "";
        if (!collider.HasMeta("slot"))
        {
            return false;
        }

        var type = (PartType)collider.GetMeta("slot").AsInt32();
        if (!_installed.TryGetValue(type, out InstalledPart entry))
        {
            return false;
        }

        _selected = type;
        message = Describe(entry);
        return true;
    }

    public PcPart? RemoveSelected()
    {
        if (_selected == null || !_installed.TryGetValue(_selected.Value, out InstalledPart entry))
        {
            return null;
        }

        _installed.Remove(_selected.Value);
        _selected = null;
        entry.Visual.QueueFree();
        return entry.Part;
    }

    public string HammerSelected()
    {
        LastHammerLanded = false;
        if (_selected == null || !_installed.TryGetValue(_selected.Value, out InstalledPart entry))
        {
            return "Сначала тапни по детали на столе — молоток сам цель не выбирает.";
        }

        if (!entry.NeedsHammer)
        {
            return $"«{entry.Part.Name}» и так сидит в слоте. Молоток по ней — это уже не ремонт, а месть.";
        }

        LastHammerLanded = true;
        SpawnHammerSparks(entry);
        entry.HammerHits++;
        Input.VibrateHandheld(90);

        float hits = entry.HammerHits;
        entry.Visual.Scale = new Vector3(1.0f + 0.13f * hits, 1.0f - 0.16f * hits, 1.0f + 0.10f * hits);
        entry.Visual.RotationDegrees = new Vector3(0, 24f - 10f * hits, 7f - 3f * hits);

        Tween tween = CreateTween();
        Vector3 basePos = entry.Visual.Position;
        tween.TweenProperty(entry.Visual, "position", basePos + new Vector3(0, 0.10f, 0), 0.06);
        tween.TweenProperty(entry.Visual, "position", basePos, 0.08);

        if (entry.HammerHits >= HammerHitsRequired)
        {
            entry.NeedsHammer = false;
            entry.Hammered = true;
            entry.Visual.RotationDegrees = new Vector3(0, 6f, 2f);
            Vector3 slot = SlotPositions[entry.Part.Type];
            Tween move = CreateTween();
            move.TweenProperty(entry.Visual, "position", slot, 0.25).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
            return $"БАМ! «{entry.Part.Name}» вбита в слот. Теперь она деталь. Формально. Греться будет сильнее, но кого это волновало.";
        }

        return $"БАМ! «{entry.Part.Name}» зашла наполовину. Ещё один удар — и геометрия сдастся.";
    }

    // ---------- Майонез (термопаста) ----------

    /// <summary>Води пальцем по процессору: возвращает прогресс, если свайп попал по CPU.</summary>
    public bool ApplyPasteSwipe(Vector2 screenPosition, Camera3D camera, float swipePixels)
    {
        if (!_installed.TryGetValue(PartType.Cpu, out InstalledPart cpu) || cpu.NeedsHammer)
        {
            return false;
        }

        Vector2 cpuScreen = camera.UnprojectPosition(cpu.Visual.GlobalPosition + new Vector3(0, 0.35f, 0));
        if (screenPosition.DistanceTo(cpuScreen) > 150f)
        {
            return false;
        }

        cpu.Paste01 = Mathf.Clamp(cpu.Paste01 + swipePixels / 320.0f, 0f, 1f);
        cpu.MayoVisual.Scale = Vector3.One * Mathf.Max(0.05f, cpu.Paste01);
        return true;
    }

    public bool CpuHasPaste =>
        _installed.TryGetValue(PartType.Cpu, out InstalledPart cpu) && cpu.Paste01 >= 0.7f;

    /// <summary>Майонез потёк на материнку при мини-игре: следующий тест — короткое замыкание.</summary>
    public bool MayoShortCircuit { get; private set; }

    /// <summary>Принимает результат мини-игры «Намазать майонез» (покрытие и потёки в процентах).</summary>
    public string SetPasteResult(float coveragePercent, float leakPercent)
    {
        if (!_installed.TryGetValue(PartType.Cpu, out InstalledPart cpu))
        {
            return "Мазать нечего: процессора на столе нет.";
        }

        cpu.Paste01 = Mathf.Clamp(coveragePercent / 100.0f, 0f, 1f);
        cpu.MayoVisual.Scale = Vector3.One * Mathf.Max(0.05f, cpu.Paste01);
        MayoShortCircuit = leakPercent > 8.0f || coveragePercent > 97.0f;

        if (MayoShortCircuit)
        {
            return $"Майонез: {coveragePercent:0}% покрытия и потёки {leakPercent:0}%. Он уже на дорожках материнки. Следующий запуск, скорее всего, будет с фейерверком.";
        }

        if (coveragePercent < 70.0f)
        {
            return $"Майонез: всего {coveragePercent:0}% покрытия. Процессор будет греться, как печка в июле. Надо 70-90%.";
        }

        if (coveragePercent > 90.0f)
        {
            return $"Майонез: {coveragePercent:0}% — щедро, на грани потёка, но дорожки сухие. Жить будет. Наверное.";
        }

        return $"Майонез: {coveragePercent:0}% — идеальный слой, как в заводской столовой. Процессор доволен.";
    }

    /// <summary>Сколько иранских деталей стоит на столе и все ли три на месте.</summary>
    public (int installed, bool fullSet) GetIranianStatus()
    {
        int count = 0;
        foreach (InstalledPart entry in _installed.Values)
        {
            if (entry.Part.Iranian)
            {
                count++;
            }
        }

        bool full = _installed.TryGetValue(PartType.Cpu, out InstalledPart cpu) && cpu.Part.Iranian
            && _installed.TryGetValue(PartType.Psu, out InstalledPart psu) && psu.Part.Iranian
            && _installed.TryGetValue(PartType.Motherboard, out InstalledPart mobo) && mobo.Part.Iranian;
        return (count, full);
    }

    public float CpuPaste01 =>
        _installed.TryGetValue(PartType.Cpu, out InstalledPart cpu) ? cpu.Paste01 : 0f;

    // ---------- Продувка и тряска ----------

    /// <summary>Продувка: сносит пыль со всех деталей. Возвращает true, если пыль ещё была.</summary>
    public bool ApplyBlow(double delta)
    {
        bool anyDust = false;
        foreach (InstalledPart entry in _installed.Values)
        {
            if (entry.Dust01 <= 0.01f)
            {
                continue;
            }

            anyDust = true;
            entry.Dust01 = Mathf.Max(0f, entry.Dust01 - (float)delta * 0.45f * BlowDustMultiplier);
            entry.DustVisual.Scale = Vector3.One * Mathf.Max(0.03f, entry.Dust01);
            if (_rng.NextDouble() < delta * 6.0)
            {
                SpawnDustPuff(entry.Visual.GlobalPosition);
            }
        }

        return anyDust;
    }

    public (PcPart? popped, string message) HandleShake(float strength)
    {
        if (_installed.Count == 0)
        {
            return (null, "Трясти нечего — стол пуст, как холодильник в конце месяца.");
        }

        foreach (InstalledPart entry in _installed.Values)
        {
            Vector3 home = entry.NeedsHammer ? PendingPosition : SlotPositions[entry.Part.Type];
            entry.Visual.Position = home + new Vector3((_rng.NextSingle() - 0.5f) * 0.05f, 0.01f, (_rng.NextSingle() - 0.5f) * 0.05f);
        }

        var candidates = new List<InstalledPart>();
        foreach (InstalledPart entry in _installed.Values)
        {
            if (entry.Part.Type != PartType.ComputerCase &&
                (entry.Hammered || entry.Part.Quality is PartQuality.Trash or PartQuality.Fake))
            {
                candidates.Add(entry);
            }
        }

        if (candidates.Count > 0 && _rng.NextDouble() < 0.16 + strength * 0.01)
        {
            InstalledPart loose = candidates[_rng.Next(candidates.Count)];
            _installed.Remove(loose.Part.Type);
            if (_selected == loose.Part.Type)
            {
                _selected = null;
            }

            loose.Visual.QueueFree();
            return (loose.Part, $"От тряски «{loose.Part.Name}» ВЫЛЕТЕЛА из корпуса и вернулась в инвентарь. А ты говорил — на скотче не держатся.");
        }

        string gremlin = candidates.Count > 0
            ? $"Внутри что-то гремит: «{candidates[_rng.Next(candidates.Count)].Part.Name}». Но держится. На честном слове."
            : "Потряс ПК — внутри тишина. Подозрительная, непривычная тишина.";
        return (null, gremlin);
    }

    // ---------- Тепло и состояние ----------

    public List<string> GetMissingEssentials()
    {
        var missing = new List<string>();
        (PartType type, string label)[] required =
        {
            (PartType.ComputerCase, "корпус"),
            (PartType.Motherboard, "материнская плата"),
            (PartType.Cpu, "процессор"),
            (PartType.Cooler, "кулер"),
            (PartType.Psu, "блок питания"),
            (PartType.Ram, "оперативная память"),
        };

        foreach ((PartType type, string label) in required)
        {
            if (!_installed.TryGetValue(type, out InstalledPart entry))
            {
                missing.Add(label);
            }
            else if (entry.NeedsHammer)
            {
                missing.Add($"{label} (не вбита молотком)");
            }
        }

        return missing;
    }

    /// <summary>Скорость нагрева °C/сек по текущему состоянию сборки.</summary>
    public float GetHeatRatePerSecond()
    {
        float rate = 1.05f;
        float dustSum = 0f;

        foreach (InstalledPart entry in _installed.Values)
        {
            dustSum += entry.Dust01;
            rate += entry.Part.Quality switch
            {
                PartQuality.Trash => 0.34f,
                PartQuality.Fake => 0.42f,
                PartQuality.Worn => 0.16f,
                _ => 0.05f
            };
            if (entry.Hammered)
            {
                rate += 0.30f;
            }
        }

        if (_installed.Count > 0)
        {
            rate += dustSum / _installed.Count * 1.15f;
        }

        if (_installed.ContainsKey(PartType.Cpu))
        {
            rate += CpuHasPaste ? (HasRealPaste ? -1.05f : -0.55f) : 1.35f;
        }

        (int iranianCount, bool iranianFull) = GetIranianStatus();
        rate += iranianFull ? 0.45f : iranianCount * 0.35f;

        if (!_installed.ContainsKey(PartType.Gpu))
        {
            rate += 0.15f;
        }

        return Mathf.Max(0.35f, rate);
    }

    /// <summary>События и время добавляют пыли всем деталям сразу (кот, дождь, жизнь).</summary>
    public void AddDustToAll(float fraction)
    {
        foreach (InstalledPart entry in _installed.Values)
        {
            entry.Dust01 = Mathf.Clamp(entry.Dust01 + fraction, 0f, 1f);
            entry.DustVisual.Scale = Vector3.One * Mathf.Max(0.03f, entry.Dust01);
        }
    }

    public void SetFanSpinning(bool spinning) => _fanSpinning = spinning;
    public void SetSmoke(bool active) => _smokeActive = active;

    public void MarkTestResult(bool passed)
    {
        foreach (InstalledPart entry in _installed.Values)
        {
            entry.TestPassed = passed;
        }

        if (!passed)
        {
            for (int i = 0; i < 10; i++)
            {
                SpawnSmokePuff();
            }

            SpawnExplosionBurst();
        }
    }

    /// <summary>
    /// Искры и пыль из-под молотка (3.0/Модуль 2): короткий жёлтый фонтанчик
    /// и честное серое облачко. Живут меньше секунды, память не держат.
    /// </summary>
    private void SpawnHammerSparks(InstalledPart entry)
    {
        Vector3 from = entry.Visual.Position + new Vector3(0, 0.3f, 0);
        SpawnBurst(from, new Color(1.0f, 0.78f, 0.30f), 16, 2.6f, 4.2f, -8.0f, 0.035f, 0.4f);
        SpawnBurst(from, new Color(0.55f, 0.48f, 0.38f, 0.85f), 8, 0.7f, 1.4f, -2.0f, 0.09f, 0.55f);
    }

    private void SpawnBurst(Vector3 from, Color color, int amount, float velocityMin, float velocityMax,
        float gravityY, float scale, float lifetime)
    {
        var material = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 1, 0),
            Spread = 38f,
            InitialVelocityMin = velocityMin,
            InitialVelocityMax = velocityMax,
            Gravity = new Vector3(0, gravityY, 0),
            ScaleMin = scale * 0.7f,
            ScaleMax = scale * 1.3f,
            Color = color
        };

        var mesh = new BoxMesh { Size = new Vector3(scale, scale, scale) };
        mesh.Material = PixelTextureFactory.Flat(color, 0.6f);

        var particles = new GpuParticles3D
        {
            Amount = amount,
            Lifetime = lifetime,
            OneShot = true,
            Explosiveness = 0.9f,
            ProcessMaterial = material,
            DrawPass1 = mesh,
            Position = from
        };
        AddChild(particles);
        particles.Emitting = true;

        Tween tween = CreateTween();
        tween.TweenInterval(lifetime + 0.3f);
        tween.TweenCallback(Callable.From(() => particles.QueueFree()));
    }

    /// <summary>
    /// Частицы взрыва (2.0/Модуль 3): осколки, искры и вспышка. Никакого неона —
    /// только честный оранжевый бада-бум и серая копоть.
    /// </summary>
    private void SpawnExplosionBurst()
    {
        Vector3 from = _installed.TryGetValue(PartType.Cpu, out InstalledPart cpu)
            ? ToLocal(cpu.Visual.GlobalPosition) + new Vector3(0, 0.5f, 0)
            : new Vector3(0, 0.6f, 0);

        var material = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 1, 0),
            Spread = 55f,
            InitialVelocityMin = 3.0f,
            InitialVelocityMax = 6.5f,
            Gravity = new Vector3(0, -6.5f, 0),
            ScaleMin = 0.05f,
            ScaleMax = 0.13f,
            Color = new Color(0.95f, 0.55f, 0.20f)
        };

        var mesh = new BoxMesh { Size = new Vector3(0.09f, 0.09f, 0.09f) };
        mesh.Material = PixelTextureFactory.Flat(new Color(0.85f, 0.45f, 0.18f), 0.7f);

        var particles = new GpuParticles3D
        {
            Amount = 56,
            Lifetime = 0.9f,
            OneShot = true,
            Explosiveness = 0.92f,
            ProcessMaterial = material,
            DrawPass1 = mesh,
            Position = from
        };
        AddChild(particles);
        particles.Emitting = true;

        var flash = new OmniLight3D
        {
            LightColor = new Color(1.0f, 0.62f, 0.28f),
            LightEnergy = 3.2f,
            OmniRange = 6.0f,
            Position = from + new Vector3(0, 0.6f, 0)
        };
        AddChild(flash);

        Tween tween = CreateTween();
        tween.TweenProperty(flash, "light_energy", 0.0, 0.45);
        tween.TweenCallback(Callable.From(() =>
        {
            flash.QueueFree();
            particles.QueueFree();
        }));
    }

    public string GetSummary()
    {
        if (_installed.Count == 0)
        {
            return "Стол пуст. Поставь корпус из инвентаря кнопкой «На стол» и тащи детали сюда.";
        }

        var lines = new List<string>();
        foreach (KeyValuePair<PartType, InstalledPart> pair in _installed)
        {
            lines.Add(Describe(pair.Value));
        }

        return string.Join("\n", lines);
    }

    public string DescribeSelected()
    {
        if (_selected != null && _installed.TryGetValue(_selected.Value, out InstalledPart entry))
        {
            return Describe(entry);
        }

        return "Ничего не выбрано. Тапни по детали на столе.";
    }

    private static string Describe(InstalledPart entry)
    {
        string state = entry.NeedsHammer
            ? "НЕ ВЛЕЗЛА — ждёт молотка"
            : entry.Hammered ? "вбита молотком" : "установлена";
        string paste = entry.Part.Type == PartType.Cpu
            ? $", майонез: {Mathf.RoundToInt(entry.Paste01 * 100)}%" : "";
        return $"{entry.Part.TypeName}: {entry.Part.Name} — {state}, пыль {Mathf.RoundToInt(entry.Dust01 * 100)}%{paste}";
    }

    // ---------- Продажа, пожар и сохранения (Модуль 3) ----------

    /// <summary>Суммарная цена установленных деталей; вбитые молотком ценятся ниже.</summary>
    public int GetTotalValueRub()
    {
        int sum = 0;
        foreach (InstalledPart entry in _installed.Values)
        {
            sum += entry.Hammered && !KnifeProtectsValue ? (int)(entry.Part.ValueRub * 0.8f) : entry.Part.ValueRub;
        }

        return sum;
    }

    /// <summary>ПК уехал к клиенту: стол пустеет, детали из инвентаря не возвращаются.</summary>
    public void ClearBuild()
    {
        CancelPlacement();
        foreach (InstalledPart entry in _installed.Values)
        {
            entry.Visual.QueueFree();
        }

        _installed.Clear();
        _selected = null;
        SetFanSpinning(false);
        SetSmoke(false);
    }

    /// <summary>После пожара одна случайная деталь превращается в уголёк и исчезает.</summary>
    public PcPart? BurnRandomPart()
    {
        var keys = new List<PartType>(_installed.Keys);
        if (keys.Count == 0)
        {
            return null;
        }

        PartType type = keys[_rng.Next(keys.Count)];
        InstalledPart entry = _installed[type];
        _installed.Remove(type);
        if (_selected == type)
        {
            _selected = null;
        }

        entry.Visual.QueueFree();
        return entry.Part;
    }

    /// <summary>Ставит деталь из сейва без броска на «влезет/не влезет» — как было, так и было.</summary>
    public void RestoreInstalled(PcPart part, bool needsHammer, bool hammered, int hammerHits, float dust01, float paste01)
    {
        if (_installed.ContainsKey(part.Type))
        {
            return;
        }

        var entry = new InstalledPart
        {
            Part = part,
            Visual = PartVisualFactory.Create(part),
            Dust01 = dust01,
            Paste01 = paste01,
            NeedsHammer = needsHammer,
            Hammered = hammered,
            HammerHits = hammerHits
        };

        entry.Visual.Position = needsHammer ? PendingPosition : SlotPositions[part.Type];
        if (needsHammer)
        {
            entry.Visual.RotationDegrees = new Vector3(0, 24f, 7f);
        }
        else if (hammered)
        {
            entry.Visual.Scale = new Vector3(1.26f, 0.68f, 1.20f);
            entry.Visual.RotationDegrees = new Vector3(0, 6f, 2f);
        }

        entry.DustVisual = BuildDustOverlay(entry.Dust01);
        entry.Visual.AddChild(entry.DustVisual);
        if (part.Type == PartType.Cpu)
        {
            entry.MayoVisual = BuildMayoOverlay();
            entry.MayoVisual.Scale = Vector3.One * Mathf.Max(0.05f, paste01);
            entry.Visual.AddChild(entry.MayoVisual);
        }

        AddChild(entry.Visual);
        AttachCollider(entry);
        _installed[part.Type] = entry;
    }

    // ---------- Служебное ----------

    private bool TryScreenToTable(Vector2 screenPosition, Camera3D camera, out Vector3 local)
    {
        local = Vector3.Zero;
        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 dir = camera.ProjectRayNormal(screenPosition);
        if (Mathf.Abs(dir.Y) < 0.0001f)
        {
            return false;
        }

        float t = (GlobalPosition.Y - origin.Y) / dir.Y;
        if (t < 0)
        {
            return false;
        }

        Vector3 world = origin + dir * t;
        local = ToLocal(world);
        return true;
    }

    private void AttachCollider(InstalledPart entry)
    {
        var body = new StaticBody3D { Name = "PartCollider" };
        body.AddToGroup(PartGroupName);
        body.SetMeta("slot", (int)entry.Part.Type);
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.05f, 0.95f, 1.05f) },
            Position = new Vector3(0, 0.45f, 0)
        });
        entry.Visual.AddChild(body);
    }

    private static Node3D BuildDustOverlay(float dust01)
    {
        var root = new Node3D { Name = "Dust", Scale = Vector3.One * Mathf.Max(0.05f, dust01) };
        var rng = new Random(99);
        StandardMaterial3D dustMat = PixelTextureFactory.Flat(new Color(0.52f, 0.48f, 0.40f, 0.85f));
        ((StandardMaterial3D)dustMat).Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        for (int i = 0; i < 5; i++)
        {
            MeshInstance3D clump = PartVisualFactory.Box($"Clump{i}",
                new Vector3((rng.NextSingle() - 0.5f) * 0.5f, 0.30f + rng.NextSingle() * 0.15f, (rng.NextSingle() - 0.5f) * 0.5f),
                new Vector3(0.22f, 0.10f, 0.22f), dustMat);
            root.AddChild(clump);
        }

        return root;
    }

    private static Node3D BuildMayoOverlay()
    {
        var root = new Node3D { Name = "Mayo", Scale = Vector3.One * 0.05f };
        StandardMaterial3D mayo = PixelTextureFactory.Flat(new Color(0.96f, 0.90f, 0.68f), 0.35f);
        root.AddChild(PartVisualFactory.Cylinder("MayoBlob", new Vector3(0, 0.22f, 0), 0.20f, 0.07f, mayo));
        root.AddChild(PartVisualFactory.Cylinder("MayoBlob2", new Vector3(0.12f, 0.23f, 0.08f), 0.12f, 0.06f, mayo));
        return root;
    }

    private void SpawnDustPuff(Vector3 near)
    {
        MeshInstance3D puff = PartVisualFactory.Box("DustPuff", ToLocal(near) + new Vector3(0, 0.5f, 0),
            new Vector3(0.18f, 0.14f, 0.18f), PixelTextureFactory.Flat(new Color(0.62f, 0.58f, 0.50f, 0.8f)));
        AddChild(puff);
        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(puff, "position", puff.Position + new Vector3(0.4f, 0.7f, 0.2f), 0.7);
        tween.TweenProperty(puff, "scale", Vector3.One * 0.2f, 0.7);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => puff.QueueFree()));
    }

    private void SpawnSmokePuff()
    {
        Vector3 from = _installed.TryGetValue(PartType.Cpu, out InstalledPart cpu)
            ? ToLocal(cpu.Visual.GlobalPosition) : new Vector3(0, 0.4f, 0);
        MeshInstance3D puff = PartVisualFactory.Box("Smoke", from + new Vector3((_rng.NextSingle() - 0.5f) * 0.4f, 0.8f, 0),
            new Vector3(0.30f, 0.24f, 0.30f), PixelTextureFactory.Flat(new Color(0.23f, 0.22f, 0.21f, 0.85f)));
        AddChild(puff);
        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(puff, "position", puff.Position + new Vector3(0.15f, 1.3f, 0.1f), 1.0);
        tween.TweenProperty(puff, "scale", Vector3.One * 2.2f, 1.0);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => puff.QueueFree()));
    }

    private static void SetGhostMaterial(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.MaterialOverride is StandardMaterial3D mat)
            {
                var copy = (StandardMaterial3D)mat.Duplicate();
                Color c = copy.AlbedoColor;
                copy.AlbedoColor = new Color(c.R, c.G, c.B, 0.55f);
                copy.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mesh.MaterialOverride = copy;
            }

            if (child is Label3D label)
            {
                label.Visible = false;
            }

            SetGhostMaterial(child);
        }
    }

    private void BuildTable()
    {
        AddChild(PartVisualFactory.Box("AssemblyTableTop", new Vector3(0, -0.09f, 0),
            new Vector3(5.2f, 0.18f, 2.6f), PixelTextureFactory.Wood()));
        AddChild(PartVisualFactory.Box("AssemblyTableLegL", new Vector3(-2.4f, -0.60f, -1.1f),
            new Vector3(0.18f, 0.9f, 0.18f), PixelTextureFactory.DarkMetal()));
        AddChild(PartVisualFactory.Box("AssemblyTableLegR", new Vector3(2.4f, -0.60f, -1.1f),
            new Vector3(0.18f, 0.9f, 0.18f), PixelTextureFactory.DarkMetal()));
        AddChild(PartVisualFactory.Box("AssemblyTableLegL2", new Vector3(-2.4f, -0.60f, 1.1f),
            new Vector3(0.18f, 0.9f, 0.18f), PixelTextureFactory.DarkMetal()));
        AddChild(PartVisualFactory.Box("AssemblyTableLegR2", new Vector3(2.4f, -0.60f, 1.1f),
            new Vector3(0.18f, 0.9f, 0.18f), PixelTextureFactory.DarkMetal()));

        var sign = new Label3D
        {
            Text = "СТОЛ СБОРКИ\nне есть с него. уже пробовали",
            PixelSize = 0.006f,
            FontSize = 38,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(0.90f, 0.84f, 0.66f),
            OutlineSize = 8,
            OutlineModulate = new Color(0.07f, 0.06f, 0.05f),
            Position = new Vector3(0, 1.55f, -0.9f)
        };
        AddChild(sign);
    }
}
