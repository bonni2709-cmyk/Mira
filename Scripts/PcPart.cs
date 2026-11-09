using Godot;

namespace TrashPcTycoon;

/// <summary>Тип комплектующего (или честного мусора).</summary>
public enum PartType
{
    Cpu,
    Gpu,
    Motherboard,
    Ram,
    Psu,
    Hdd,
    ComputerCase,
    Cooler,
    Ssd,
    Junk,
    // 2.0/Модуль 2: добавляется ТОЛЬКО в конец — тип сериализуется в сейв числом.
    RgbStrip
}

/// <summary>Состояние детали: от «выкопал и уже стыдно» до «почти честная подделка».</summary>
public enum PartQuality
{
    Trash,
    Worn,
    Working,
    Fake
}

/// <summary>
/// Одна деталь ПК. Чистые данные без сцены: 3D-представление строит PartVisualFactory,
/// хранение и деньги ведёт GameState, случайную выдачу — PartCatalog.
/// </summary>
public sealed class PcPart
{
    public PcPart(string name, PartType type, PartQuality quality, int valueRub,
        Color tint, string source, string description, bool iranian = false)
    {
        Name = name;
        Type = type;
        Quality = quality;
        ValueRub = valueRub;
        Tint = tint;
        Source = source;
        Description = description;
        Iranian = iranian;
    }

    /// <summary>Деталь спецзаказа «Иранская сборка»: работает от автобатареи и на честном слове.</summary>
    public bool Iranian { get; }

    public string Name { get; }
    public PartType Type { get; }
    public PartQuality Quality { get; }
    public int ValueRub { get; }
    public Color Tint { get; }
    public string Source { get; }
    public string Description { get; }

    public string TypeName => Type switch
    {
        PartType.Cpu => "Процессор",
        PartType.Gpu => "Видеокарта",
        PartType.Motherboard => "Материнская плата",
        PartType.Ram => "Оперативная память",
        PartType.Psu => "Блок питания",
        PartType.Hdd => "Жёсткий диск",
        PartType.ComputerCase => "Корпус",
        PartType.Cooler => "Кулер",
        PartType.Ssd => "SSD",
        PartType.RgbStrip => "RGB-подсветка",
        _ => "Хлам"
    };

    public string QualityName => Quality switch
    {
        PartQuality.Trash => "убитая",
        PartQuality.Worn => "поношенная",
        PartQuality.Working => "рабочая",
        PartQuality.Fake => "фейковая",
        _ => "непонятная"
    };

    public string InventoryLine => $"{Name} — {TypeName}, {QualityName}, сдать можно за {ValueRub} ₽";
}
