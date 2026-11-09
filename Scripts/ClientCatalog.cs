using System;

namespace TrashPcTycoon;

/// <summary>Один клиент: школьник с копилкой и мечтой о Киберпанке на ультрах.</summary>
public sealed class ClientData
{
    public required string Name;
    public required string IntroLine;
    public required int BudgetRub;
    public required Godot.Color HoodieColor;
    public required Godot.Color CapColor;
}

public static class ClientCatalog
{
    private static readonly (string Name, string Intro, Godot.Color Hoodie, Godot.Color Cap)[] Pool =
    {
        ("Даня, 12 лет", "Мне нужен комп, чтобы Киберпанк на УЛЬТРАХ. У меня RTX… ну, будет. Наверное.",
            new Godot.Color(0.45f, 0.30f, 0.22f), new Godot.Color(0.55f, 0.25f, 0.18f)),
        ("Кирилл, 13 лет", "Папа сказал «купишь сам — играй сам». Вот, копил с обедов. Киберпанк потянет?",
            new Godot.Color(0.28f, 0.38f, 0.30f), new Godot.Color(0.30f, 0.30f, 0.34f)),
        ("Артём, 11 лет", "У брата комп тянет Майнкрафт с шейдерами. Мне надо, чтобы Киберпанк. Чтобы утереть.",
            new Godot.Color(0.38f, 0.34f, 0.45f), new Godot.Color(0.60f, 0.50f, 0.25f)),
        ("Влад, 14 лет", "Я стример. Ну, буду. Канал уже придумал. Нужен комп под Киберпанк и стрим одновременно.",
            new Godot.Color(0.30f, 0.30f, 0.30f), new Godot.Color(0.45f, 0.45f, 0.42f)),
        ("Матвей, 12 лет", "Продал все скины. Ну почти. Бабушка добавила. Киберпанк на максимуме — это святое.",
            new Godot.Color(0.50f, 0.42f, 0.26f), new Godot.Color(0.25f, 0.35f, 0.40f)),
    };

    public const string RequestLine =
        "Запусти Киберпанк прямо при мне. Если продержится минуту и не задымится — плачу. Если задымится… я уйду. Быстро.";

    public const string WaitingLine =
        "Я подожду. Только это… почему от корпуса пахнет майонезом и горелым? А, ладно, не отвечай.";

    public static ClientData CreateRandom(Random rng)
    {
        (string name, string intro, Godot.Color hoodie, Godot.Color cap) = Pool[rng.Next(Pool.Length)];
        return new ClientData
        {
            Name = name,
            IntroLine = intro,
            BudgetRub = rng.Next(900, 1901),
            HoodieColor = hoodie,
            CapColor = cap
        };
    }
}
