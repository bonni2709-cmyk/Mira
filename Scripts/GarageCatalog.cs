namespace TrashPcTycoon;

/// <summary>
/// Уровни гаража (Модуль 4): покупаются один раз, открывают богатых клиентов,
/// дают бонус к цене продажи и меняют вид помещения (см. GarageBuilder.ApplyLevel).
/// </summary>
public static class GarageCatalog
{
    public sealed record LevelDef(int Level, string Name, int PriceRub, string Description);

    public static readonly LevelDef[] Levels =
    {
        new(1, "Гараж дяди Вити", 0, "Одна лампочка, свалка под боком, клиенты — школьники и бабушки."),
        new(2, "Свой угол", 1500, "Постер, стеллаж и нормальный стул. Заходят дяди из бухгалтерии, цены +10%."),
        new(3, "Мастерская «Кулибин и Ко»", 4000, "Стенд с инструментами и вывеска. Заходят стримеры, цены +20%, находки богаче."),
    };

    public static LevelDef? Next(int currentLevel)
    {
        foreach (LevelDef def in Levels)
        {
            if (def.Level == currentLevel + 1)
            {
                return def;
            }
        }

        return null;
    }

    /// <summary>Множитель цены продажи готового ПК по уровню гаража.</summary>
    public static float SalePriceMultiplier(int level) => level switch
    {
        >= 3 => 1.20f,
        2 => 1.10f,
        _ => 1.0f
    };

    /// <summary>Множитель бюджетов клиентов: чем солиднее гараж, тем толще копилки заходят.</summary>
    public static float ClientBudgetMultiplier(int level) => level switch
    {
        >= 3 => 1.25f,
        2 => 1.10f,
        _ => 1.0f
    };
}
