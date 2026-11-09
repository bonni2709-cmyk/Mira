using System;
using Godot;

namespace TrashPcTycoon;

/// <summary>Головной убор / причёска клиента — ClientController показывает нужный вариант.</summary>
public enum ClientHat
{
    Cap,
    Beanie,
    GrayHair,
    None
}

/// <summary>
/// Один клиент (Модуль 4: не только школьники): архетип, мечта-игра, копилка,
/// особые требования вроде «только чтобы не шумело» (потолок температуры).
/// </summary>
public sealed class ClientData
{
    public required string Name;
    public required string Archetype;
    public required string GameTitle;
    public required string IntroLine;
    public required string RequestLine;
    public required int BudgetRub;
    public required Color HoodieColor;
    public required Color CapColor;
    public required ClientHat Hat;
    public float? MaxTempCelsius;
    /// <summary>Спецзаказ 2.0: собрать ПК целиком из иранского набора деталей.</summary>
    public bool IsIranianOrder;
}

public static class ClientCatalog
{
    public const string WaitingLine =
        "Я подожду. Только это… почему от корпуса пахнет майонезом и горелым? А, ладно, не отвечай.";

    private sealed record Archetype(string Name, string[] PersonNames, string Game, string Intro,
        int BudgetMin, int BudgetMax, Color Hoodie, Color Cap, ClientHat Hat, float? MaxTemp, int MinGarageLevel);

    private static readonly Archetype[] Pool =
    {
        new("Школьник",
            new[] { "Даня, 12 лет", "Кирилл, 13 лет", "Артём, 11 лет", "Матвей, 12 лет" },
            "Киберпанк 2077",
            "Мне нужен комп, чтобы Киберпанк на УЛЬТРАХ. У меня RTX… ну, будет. Наверное.",
            900, 1900, new Color(0.45f, 0.30f, 0.22f), new Color(0.55f, 0.25f, 0.18f), ClientHat.Cap, null, 1),
        new("Студент",
            new[] { "Игорь, 19 лет", "Студент Тимур, 20 лет", "Лёха с потока, 18 лет" },
            "Ведьмак 3 на ультрах",
            "Сессия закрыта, стипендия получена. Хочу Ведьмака на ультрах, а не слайд-шоу, как сейчас.",
            1400, 2600, new Color(0.28f, 0.38f, 0.30f), new Color(0.30f, 0.30f, 0.34f), ClientHat.Beanie, null, 1),
        new("Бабушка",
            new[] { "Бабушка Зина", "Бабушка Валя", "Зинаида Петровна" },
            "Одноклассники и пасьянс «Косынка»",
            "Внучок, мне бы компьютер: одноклассники смотреть и пасьянс. Только чтобы НЕ ШУМЕЛ, как тот пылесос у соседки.",
            1200, 2200, new Color(0.50f, 0.38f, 0.42f), new Color(0.72f, 0.70f, 0.68f), ClientHat.GrayHair, 88f, 1),
        new("Дядя из бухгалтерии",
            new[] { "Олег Иванович, бухгалтерия", "Дядя Олег, 45 лет" },
            "World of Tanks и 1С без тормозов",
            "Домашний комп помер перед отчётным кварталом. Надо, чтобы танки тянуло и 1С не думала по минуте.",
            2000, 3500, new Color(0.32f, 0.34f, 0.38f), new Color(0.25f, 0.25f, 0.28f), ClientHat.None, null, 2),
        new("Стример",
            new[] { "Стример Влад", "Влад «почти тысяча подписчиков»" },
            "Киберпанк и стрим одновременно",
            "Я стример. Ну, буду. Канал уже придумал, логотип нарисовал. Нужен комп под Киберпанк и стрим в 1080p.",
            2500, 4200, new Color(0.30f, 0.30f, 0.30f), new Color(0.45f, 0.45f, 0.42f), ClientHat.Cap, null, 3),
    };

    public static ClientData CreateRandom(Random rng, int garageLevel, float budgetMultiplier)
    {
        // Спецзаказ «Иранская сборка» (2.0): редкий гость с автомобильным аккумулятором.
        if (rng.Next(100) < 14)
        {
            return new ClientData
            {
                Name = "Ашот с авторынка",
                Archetype = "Спецзаказ",
                GameTitle = "Иранская сборка от аккумулятора",
                IntroLine = "Слушай, брат. Нужен компьютер, который работает от автомобильного аккумулятора. У меня как раз есть иранский комплект: провода от холодильника ЗИЛ, блок питания из микроволновки и процессор на керосине. Всё уже у тебя в инвентаре будет. Собери ТОЛЬКО из них, слышишь? Плачу ×1.8. Но если накосячишь — при тесте скачок напряжения и взрыв, я предупредил.",
                RequestLine = "Поставь все три иранские детали: материнку ЗИЛ, БП из микроволновки и керосиновый процессор. Запусти тест при мне. Взлетим — сам виноват.",
                BudgetRub = (int)(2600 * budgetMultiplier),
                HoodieColor = new Color(0.30f, 0.30f, 0.30f),
                CapColor = new Color(0.45f, 0.45f, 0.42f),
                Hat = ClientHat.Cap,
                MaxTempCelsius = null,
                IsIranianOrder = true
            };
        }

        var available = new System.Collections.Generic.List<Archetype>();
        foreach (Archetype a in Pool)
        {
            if (a.MinGarageLevel <= garageLevel)
            {
                available.Add(a);
            }
        }

        Archetype arch = available[rng.Next(available.Count)];
        string person = arch.PersonNames[rng.Next(arch.PersonNames.Length)];
        int budget = (int)(rng.Next(arch.BudgetMin, arch.BudgetMax + 1) * budgetMultiplier);

        string request = arch.MaxTemp.HasValue
            ? $"Включи при мне: {arch.Game}. И чтобы не шумело! Если нагреется выше {arch.MaxTemp.Value:0} градусов — это уже не компьютер, а обогреватель, платить за обогреватель не буду."
            : $"Запусти {arch.Game} прямо при мне. Продержится минуту и не задымится — плачу. Задымится — я уйду. Быстро.";

        return new ClientData
        {
            Name = person,
            Archetype = arch.Name,
            GameTitle = arch.Game,
            IntroLine = arch.Intro,
            RequestLine = request,
            BudgetRub = budget,
            HoodieColor = arch.Hoodie,
            CapColor = arch.Cap,
            Hat = arch.Hat,
            MaxTempCelsius = arch.MaxTemp
        };
    }
}
