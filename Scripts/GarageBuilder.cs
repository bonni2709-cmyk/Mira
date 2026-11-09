using System;
using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Строит гараж из примитивов в коде: бетонный пол, две стены, верстак, полка,
/// бочка и одна лампочка под потолком. Сцену в .tscn это не засоряет, а пиксель
/// дают текстуры PixelTextureFactory с Nearest-фильтрацией.
/// </summary>
public partial class GarageBuilder : Node3D
{
    private readonly Random _flickerRng = new();
    private OmniLight3D _bulbLight = null!;
    private MeshInstance3D _bulbMesh = null!;
    private float _bulbBaseEnergy = 1.5f;
    private float _flickerTime;
    private float _dipTimer = 3f;
    private float _dipLeft;
    private Node3D _level2 = null!;
    private Node3D _level3 = null!;
    private Node3D _cat = null!;

    public override void _Ready()
    {
        BuildRoom();
        BuildBench();
        BuildDecor();
        BuildLamp();
        BuildLevelDecor();
        BuildCat();
        ApplyLevel(1);
    }

    /// <summary>Модуль 4: оформление под уровень гаража (см. GarageCatalog).</summary>
    public void ApplyLevel(int level)
    {
        _level2.Visible = level >= 2;
        _level3.Visible = level >= 3;
        _bulbBaseEnergy = level switch { >= 3 => 1.85f, 2 => 1.65f, _ => 1.5f };
        _bulbLight.LightEnergy = _bulbBaseEnergy;
    }

    /// <summary>
    /// Мерцание лампочки (4.0/Модуль 2): лёгкое дрожание яркости и редкие провалы —
    /// проводка в гараже держится на честном слове и изоленте. Во время блэкаута не трогаем.
    /// </summary>
    public override void _Process(double delta)
    {
        if (!_bulbLight.Visible)
        {
            return;
        }

        float dt = (float)delta;
        _flickerTime += dt;
        float wobble = Mathf.Sin(_flickerTime * 41f) * 0.35f + Mathf.Sin(_flickerTime * 13.7f + 1.3f) * 0.65f;
        float factor = 1.0f + wobble * 0.035f;

        _dipTimer -= dt;
        if (_dipTimer <= 0f)
        {
            _dipTimer = 2.5f + (float)_flickerRng.NextDouble() * 6.0f;
            _dipLeft = 0.06f + (float)_flickerRng.NextDouble() * 0.10f;
        }

        if (_dipLeft > 0f)
        {
            _dipLeft -= dt;
            factor *= 0.55f;
        }

        _bulbLight.LightEnergy = _bulbBaseEnergy * factor;
        if (_bulbMesh.MaterialOverride is StandardMaterial3D mat)
        {
            mat.EmissionEnergyMultiplier = 0.7f * factor;
        }
    }

    /// <summary>Событие «отключили свет»: лампочка гаснет на время, гараж не сдаётся.</summary>
    public void SetBlackout(bool blackout)
    {
        _bulbLight.Visible = !blackout;
        if (_bulbMesh.MaterialOverride is StandardMaterial3D mat)
        {
            mat.EmissionEnergyMultiplier = blackout ? 0.0f : 0.7f;
            mat.AlbedoColor = blackout ? new Color(0.35f, 0.32f, 0.28f) : new Color(1.0f, 0.82f, 0.52f);
        }
    }

    public void SetCatVisible(bool visible)
    {
        _cat.Visible = visible;
    }

    private void BuildLevelDecor()
    {
        _level2 = new Node3D { Name = "Level2Decor" };
        AddChild(_level2);
        MeshInstance3D poster = PartVisualFactory.Box("Poster", new Vector3(3.4f, 3.1f, -5.26f),
            new Vector3(1.7f, 1.1f, 0.04f), PixelTextureFactory.Flat(new Color(0.55f, 0.42f, 0.30f)));
        _level2.AddChild(poster);
        var posterText = new Label3D
        {
            Text = "СБОРКА ПК\nБЫСТРО. ДЁШЕВО. ИНОГДА РАБОТАЕТ",
            PixelSize = 0.006f,
            FontSize = 36,
            Modulate = new Color(0.30f, 0.22f, 0.14f),
            Position = new Vector3(3.4f, 3.1f, -5.20f)
        };
        _level2.AddChild(posterText);
        _level2.AddChild(PartVisualFactory.Box("CrateStack", new Vector3(5.9f, 0.5f, 0.8f),
            new Vector3(1.0f, 1.0f, 1.0f), PixelTextureFactory.Wood()));
        _level2.AddChild(PartVisualFactory.Box("Stool", new Vector3(-0.9f, 0.35f, 3.4f),
            new Vector3(0.7f, 0.12f, 0.7f), PixelTextureFactory.Wood()));

        _level3 = new Node3D { Name = "Level3Decor" };
        AddChild(_level3);
        _level3.AddChild(PartVisualFactory.Box("ToolWall", new Vector3(-6.62f, 2.7f, 0.6f),
            new Vector3(0.06f, 2.0f, 3.2f), PixelTextureFactory.DarkMetal()));
        _level3.AddChild(PartVisualFactory.Box("HangingWrench", new Vector3(-6.56f, 3.1f, -0.2f),
            new Vector3(0.05f, 0.55f, 0.14f), PixelTextureFactory.Flat(new Color(0.65f, 0.62f, 0.58f), 0.5f)));
        _level3.AddChild(PartVisualFactory.Box("HangingHammer", new Vector3(-6.56f, 2.9f, 0.5f),
            new Vector3(0.05f, 0.6f, 0.2f), PixelTextureFactory.RustMetal()));
        _level3.AddChild(PartVisualFactory.Box("Rug", new Vector3(0.4f, 0.03f, 1.1f),
            new Vector3(4.6f, 0.03f, 2.2f), PixelTextureFactory.Flat(new Color(0.42f, 0.26f, 0.20f))));
        var sign3 = new Label3D
        {
            Text = "МАСТЕРСКАЯ «КУЛИБИН И КО»\nгарантия: до первой тряски",
            PixelSize = 0.007f,
            FontSize = 40,
            Modulate = new Color(0.88f, 0.78f, 0.55f),
            OutlineSize = 8,
            OutlineModulate = new Color(0.07f, 0.06f, 0.05f),
            Position = new Vector3(-2.8f, 4.3f, -5.24f)
        };
        _level3.AddChild(sign3);
    }

    private void BuildCat()
    {
        _cat = new Node3D { Name = "GarageCat", Position = new Vector3(-4.2f, 1.15f, -3.6f), Visible = false };
        StandardMaterial3D fur = PixelTextureFactory.Flat(new Color(0.55f, 0.38f, 0.22f));
        StandardMaterial3D dark = PixelTextureFactory.Flat(new Color(0.15f, 0.13f, 0.12f));
        _cat.AddChild(PartVisualFactory.Box("CatBody", new Vector3(0, 0.14f, 0), new Vector3(0.52f, 0.26f, 0.30f), fur));
        _cat.AddChild(PartVisualFactory.Box("CatHead", new Vector3(0.34f, 0.30f, 0), new Vector3(0.26f, 0.24f, 0.26f), fur));
        _cat.AddChild(PartVisualFactory.Box("CatEarL", new Vector3(0.28f, 0.48f, -0.08f), new Vector3(0.07f, 0.12f, 0.07f), dark));
        _cat.AddChild(PartVisualFactory.Box("CatEarR", new Vector3(0.28f, 0.48f, 0.08f), new Vector3(0.07f, 0.12f, 0.07f), dark));
        _cat.AddChild(PartVisualFactory.Box("CatTail", new Vector3(-0.34f, 0.30f, 0), new Vector3(0.08f, 0.36f, 0.08f), fur));
        AddChild(_cat);
    }

    private void BuildRoom()
    {
        AddBox("Floor", new Vector3(0, -0.11f, 0), new Vector3(14f, 0.22f, 11f), PixelTextureFactory.GarageFloor());
        AddBox("BackWall", new Vector3(0, 2.5f, -5.45f), new Vector3(14f, 5.2f, 0.3f), PixelTextureFactory.Wall());
        AddBox("LeftWall", new Vector3(-6.85f, 2.5f, 0), new Vector3(0.3f, 5.2f, 11f), PixelTextureFactory.Wall());
        AddBox("BackWallSkirting", new Vector3(0, 0.18f, -5.25f), new Vector3(14f, 0.35f, 0.08f), PixelTextureFactory.DarkMetal());
        AddBox("OilStain", new Vector3(1.4f, 0.012f, 0.9f), new Vector3(2.4f, 0.02f, 1.6f),
            PixelTextureFactory.Flat(new Color(0.13f, 0.12f, 0.11f)));
    }

    private void BuildBench()
    {
        StandardMaterial3D wood = PixelTextureFactory.Wood();
        AddBox("BenchTop", new Vector3(-2.6f, 1.02f, -3.7f), new Vector3(5.2f, 0.18f, 1.7f), wood);
        AddBox("BenchLegL", new Vector3(-4.9f, 0.5f, -4.3f), new Vector3(0.18f, 1.0f, 0.18f), PixelTextureFactory.DarkMetal());
        AddBox("BenchLegR", new Vector3(-0.3f, 0.5f, -4.3f), new Vector3(0.18f, 1.0f, 0.18f), PixelTextureFactory.DarkMetal());
        AddBox("BenchLegL2", new Vector3(-4.9f, 0.5f, -3.1f), new Vector3(0.18f, 1.0f, 0.18f), PixelTextureFactory.DarkMetal());
        AddBox("BenchLegR2", new Vector3(-0.3f, 0.5f, -3.1f), new Vector3(0.18f, 1.0f, 0.18f), PixelTextureFactory.DarkMetal());

        AddBox("Shelf", new Vector3(-2.6f, 3.1f, -5.15f), new Vector3(5.4f, 0.12f, 0.7f), wood);
        AddBox("ShelfCan1", new Vector3(-4.4f, 3.42f, -5.15f), new Vector3(0.42f, 0.55f, 0.42f), PixelTextureFactory.RustMetal());
        AddBox("ShelfCan2", new Vector3(-3.7f, 3.36f, -5.1f), new Vector3(0.36f, 0.42f, 0.36f), PixelTextureFactory.DarkMetal());
        AddBox("ShelfBox", new Vector3(0.1f, 3.45f, -5.15f), new Vector3(0.95f, 0.6f, 0.55f), PixelTextureFactory.Cardboard());
    }

    private void BuildDecor()
    {
        AddBox("CardboardBox1", new Vector3(-5.6f, 0.45f, 1.8f), new Vector3(1.1f, 0.9f, 1.0f), PixelTextureFactory.Cardboard());
        AddBox("CardboardBox2", new Vector3(-5.5f, 1.25f, 1.75f), new Vector3(0.85f, 0.65f, 0.8f), PixelTextureFactory.Cardboard())
            .RotationDegrees = new Vector3(0, 18f, 0);
        AddBox("OldCaseOnFloor", new Vector3(-5.4f, 0.75f, -1.2f), new Vector3(0.85f, 1.5f, 1.6f), PixelTextureFactory.RustMetal());

        MeshInstance3D barrel = PartVisualFactory.Cylinder("Barrel", new Vector3(5.9f, 0.75f, -4.2f),
            0.62f, 1.5f, PixelTextureFactory.RustMetal());
        AddChild(barrel);

        var sign = new Label3D
        {
            Text = "ГАРАЖ ДЯДИ ВИТИ\nаренда: бутылка и обещание",
            PixelSize = 0.008f,
            FontSize = 40,
            Modulate = new Color(0.85f, 0.78f, 0.62f),
            OutlineSize = 8,
            OutlineModulate = new Color(0.07f, 0.06f, 0.05f),
            Position = new Vector3(2.8f, 3.6f, -5.25f)
        };
        AddChild(sign);
    }

    private void BuildLamp()
    {
        AddChild(PartVisualFactory.Cylinder("LampCord", new Vector3(0, 5.6f, 0), 0.03f, 1.6f,
            PixelTextureFactory.Flat(new Color(0.10f, 0.09f, 0.09f))));

        var bulbMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.82f, 0.52f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.75f, 0.42f),
            EmissionEnergyMultiplier = 0.7f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
        };
        var bulbMesh = new SphereMesh { Radius = 0.20f, Height = 0.40f, RadialSegments = 12, Rings = 6 };
        _bulbMesh = new MeshInstance3D { Name = "Bulb", Mesh = bulbMesh, Position = new Vector3(0, 4.75f, 0), MaterialOverride = bulbMaterial };
        AddChild(_bulbMesh);

        var light = new OmniLight3D
        {
            Name = "CeilingBulb",
            Position = new Vector3(0, 4.45f, 0),
            LightColor = new Color(1.0f, 0.84f, 0.60f),
            LightEnergy = 1.5f,
            OmniRange = 11.0f,
            OmniAttenuation = 1.4f,
            ShadowEnabled = false
        };
        _bulbLight = light;
        AddChild(light);

        var fill = new DirectionalLight3D
        {
            Name = "WeakFill",
            RotationDegrees = new Vector3(-48f, -32f, 0),
            LightColor = new Color(0.75f, 0.72f, 0.66f),
            LightEnergy = 0.22f,
            ShadowEnabled = false
        };
        AddChild(fill);
    }

    private MeshInstance3D AddBox(string name, Vector3 position, Vector3 size, Material material)
    {
        MeshInstance3D box = PartVisualFactory.Box(name, position, size, material);
        AddChild(box);
        return box;
    }
}
