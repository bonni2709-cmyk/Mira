using System;
using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Каталоги случайных находок: что реально выкапывается на свалке
/// и что «честно» присылает AliExpress. Никакой балансной магии вне этого файла.
/// </summary>
public static class PartCatalog
{
    public const int AliBoxPriceRub = 150;
    public const int ScrapRewardRub = 15;

    private sealed record Template(string Name, PartType Type, PartQuality Quality,
        int ValueRub, Color Tint, string Description);

    private static readonly Template[] DumpTemplates =
    {
        new("Процессор «Пентиум у деда в гараже»", PartType.Cpu, PartQuality.Worn, 90,
            new Color(0.45f, 0.48f, 0.42f), "Ножки погнуты ровно наполовину. Гордость, а не брак."),
        new("Видеокарта «Почти не грелась»", PartType.Gpu, PartQuality.Trash, 70,
            new Color(0.35f, 0.32f, 0.30f), "Вентилятор крутится, если его подтолкнуть пальцем."),
        new("Материнка с вздувшимися конденсаторами", PartType.Motherboard, PartQuality.Trash, 55,
            new Color(0.25f, 0.38f, 0.22f), "Конденсаторы смотрят на тебя с осуждением."),
        new("Планка RAM «8 ГБ (по ощущениям 4)»", PartType.Ram, PartQuality.Worn, 65,
            new Color(0.20f, 0.42f, 0.55f), "Наклейка честная. Чипы — как повезёт."),
        new("Блок питания «500 Вт китайских»", PartType.Psu, PartQuality.Trash, 60,
            new Color(0.50f, 0.48f, 0.45f), "Весит как пустая банка. Внутри, видимо, тоже пусто."),
        new("HDD, который стучит, но работает", PartType.Hdd, PartQuality.Worn, 75,
            new Color(0.55f, 0.52f, 0.48f), "Стук — это он так здоровается. Наверное."),
        new("Корпус со вмятиной «от злости»", PartType.ComputerCase, PartQuality.Worn, 80,
            new Color(0.52f, 0.46f, 0.38f), "Вмятина придаёт аэродинамику. Наверное."),
        new("Кулер на честном слове и скотче", PartType.Cooler, PartQuality.Trash, 40,
            new Color(0.60f, 0.58f, 0.55f), "Одно крепление потеряно, но энтузиазм на месте."),
        new("SSD «1 ТБ» объёмом с флешку", PartType.Ssd, PartQuality.Fake, 85,
            new Color(0.30f, 0.30f, 0.34f), "Внутри тепло и пусто, как в обещаниях продавца."),
    };

    private static readonly Template[] AliTemplates =
    {
        new("RTX 5090 Ti «Картонная серия»", PartType.Gpu, PartQuality.Fake, 220,
            new Color(0.72f, 0.55f, 0.28f), "Кулеры нарисованы маркером. Зато не греется — греться нечему."),
        new("Процессор i9-14900K «реплика 1 в 1»", PartType.Cpu, PartQuality.Fake, 200,
            new Color(0.65f, 0.60f, 0.35f), "Крышка настоящая. Под крышкой — фольга и надежда."),
        new("RAM 64 ГБ «DDR9 будущего»", PartType.Ram, PartQuality.Fake, 160,
            new Color(0.55f, 0.30f, 0.55f), "DDR9 ещё не изобрели, а она уже продаётся. Опережает время."),
        new("Блок питания 2000 Вт «Тихоня»", PartType.Psu, PartQuality.Fake, 150,
            new Color(0.40f, 0.36f, 0.32f), "2000 Вт — это суммарно за всю его жизнь."),
        new("SSD 16 ТБ «Почти как настоящий»", PartType.Ssd, PartQuality.Fake, 180,
            new Color(0.32f, 0.45f, 0.60f), "16 ТБ на стикере. По факту — карта памяти и скотч."),
        new("Материнка «Gaming Pro Max Ultra»", PartType.Motherboard, PartQuality.Fake, 170,
            new Color(0.48f, 0.25f, 0.22f), "В названии четыре слова «про», значит точно игровая."),
        new("Кулер «Водянка без воды»", PartType.Cooler, PartQuality.Fake, 110,
            new Color(0.45f, 0.55f, 0.58f), "Трубки есть. Воды нет. Смысла, впрочем, тоже."),
        new("Корпус «Игровой, с дырками»", PartType.ComputerCase, PartQuality.Fake, 140,
            new Color(0.58f, 0.50f, 0.42f), "Дырки для вентиляции просверлены вручную. Криво, но от души."),
    };

    private static readonly string[] NothingLines =
    {
        "Пусто. Только пакет, который видел лучшие времена.",
        "Выкопал чужую перчатку. Она одна. Как и ты в этом бизнесе.",
        "Старая газета 2009 года. Цены там были добрее.",
        "Ничего, кроме ржавой гайки. Гайку оставил — вдруг пригодится в Модуле 2.",
    };

    private static readonly string[] ScrapLines =
    {
        "Медный провод! Сдал на цветмет.",
        "Куча болтов. Барахольщик забрал всё оптом.",
        "Радиатор от чего-то большого. Ушёл как металлолом.",
    };

    public static PcPart CreateDumpFind(Random rng)
    {
        Template t = DumpTemplates[rng.Next(DumpTemplates.Length)];
        int value = t.ValueRub + rng.Next(-15, 21);
        return new PcPart(t.Name, t.Type, t.Quality, Math.Max(10, value), t.Tint, "Свалка", t.Description);
    }

    public static PcPart CreateAliPart(Random rng)
    {
        Template t = AliTemplates[rng.Next(AliTemplates.Length)];
        int value = t.ValueRub + rng.Next(-20, 31);
        return new PcPart(t.Name, t.Type, t.Quality, Math.Max(20, value), t.Tint, "AliExpress", t.Description);
    }

    public static string RandomNothingLine(Random rng) => NothingLines[rng.Next(NothingLines.Length)];

    public static string RandomScrapLine(Random rng) => ScrapLines[rng.Next(ScrapLines.Length)];
}
