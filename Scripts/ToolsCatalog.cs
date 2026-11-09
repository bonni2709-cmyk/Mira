using System.Collections.Generic;

namespace TrashPcTycoon;

/// <summary>Инструменты, которые покупаются за заработанные деньги и навсегда меняют гаражную жизнь.</summary>
public static class ToolsCatalog
{
    public const string BetterHammer = "better_hammer";
    public const string Vacuum = "vacuum";
    public const string RealPaste = "real_paste";
    public const string DeskFan = "desk_fan";
    // Обновление 2.0, Модуль 2: нож, турбина и ящик.
    public const string SwissKnife = "swiss_knife";
    public const string Turbine = "turbine";
    public const string Crate = "crate";
    // Обновление 3.0, Модуль 3: апгрейды гаража (отдельная вкладка в магазине).
    public const string ExtractorHood = "extractor_hood";
    public const string SteelTable = "steel_table";

    public sealed record ToolDef(string Id, string Name, int PriceRub, string Description);

    public static readonly IReadOnlyList<ToolDef> All = new List<ToolDef>
    {
        new(BetterHammer, "Молоток «Убедитель»", 300,
            "Не влезло с первого удара? Теперь влезает с первого. Один удар вместо двух."),
        new(Vacuum, "Пылесос «Байкал» (б/у)", 450,
            "Продувка сдувает пыль вдвое быстрее. Ревёт, как самолёт, но дело делает."),
        new(RealPaste, "Настоящая термопаста", 250,
            "Не майонез. Шприц, серая паста, никакого салата. Охлаждает заметно лучше."),
        new(DeskFan, "Комнатный вентилятор", 600,
            "Дует на ПК весь тест без устали. Бабушкин, трёхскоростной, бессмертный."),
        new(SwissKnife, "Нож «Швейцарец»", 500,
            "Вбитые молотком детали больше не теряют 20% цены: нож подрезает скотч аккуратно, без вмятин. В комплекте открывашка и штопор — для настроения."),
        new(Turbine, "Турбина «Вихрь-3000»", 850,
            "Кнопка «Охладить» сдувает -11 °C за тап вместо -6, а на фоне турбина тянет ещё 2.2 °C/сек весь тест. Соседи думают, что ты пылесосишь. Постоянно."),
        new(Crate, "Ящик «Кладовка»", 400,
            "Инвентарь: +5 слотов (15 → 20). Место под детали, запчасти, мечты и ещё один ящик поменьше."),
    };

    /// <summary>Апгрейды самого гаража: покупаются там же, живут в том же списке владений.</summary>
    public static readonly IReadOnlyList<ToolDef> Upgrades = new List<ToolDef>
    {
        new(ExtractorHood, "Вытяжка «Циклон»", 700,
            "Во время теста стартовая температура на 5 °C ниже: дым и гарь уходят в трубу, а не в лёгкие и процессор."),
        new(SteelTable, "Стол «Нержавейка»", 550,
            "Детали на новом столе пачкаются пылью вдвое меньше, а разгон стабильнее: всплески жара случаются в два раза реже."),
    };

    public static ToolDef? Find(string id)
    {
        foreach (ToolDef def in Upgrades)
        {
            if (def.Id == id)
            {
                return def;
            }
        }

        foreach (ToolDef def in All)
        {
            if (def.Id == id)
            {
                return def;
            }
        }

        return null;
    }
}
