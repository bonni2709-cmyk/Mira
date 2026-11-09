using System.Collections.Generic;

namespace TrashPcTycoon;

/// <summary>Инструменты, которые покупаются за заработанные деньги и навсегда меняют гаражную жизнь.</summary>
public static class ToolsCatalog
{
    public const string BetterHammer = "better_hammer";
    public const string Vacuum = "vacuum";
    public const string RealPaste = "real_paste";
    public const string DeskFan = "desk_fan";

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
    };

    public static ToolDef? Find(string id)
    {
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
