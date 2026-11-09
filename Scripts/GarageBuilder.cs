using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Строит гараж из примитивов в коде: бетонный пол, две стены, верстак, полка,
/// бочка и одна лампочка под потолком. Сцену в .tscn это не засоряет, а пиксель
/// дают текстуры PixelTextureFactory с Nearest-фильтрацией.
/// </summary>
public partial class GarageBuilder : Node3D
{
    public override void _Ready()
    {
        BuildRoom();
        BuildBench();
        BuildDecor();
        BuildLamp();
    }

    private void BuildRoom()
    {
        AddBox("Floor", new Vector3(0, -0.11f, 0), new Vector3(14f, 0.22f, 11f), PixelTextureFactory.Concrete());
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
        AddChild(new MeshInstance3D { Name = "Bulb", Mesh = bulbMesh, Position = new Vector3(0, 4.75f, 0), MaterialOverride = bulbMaterial });

        var light = new OmniLight3D
        {
            Name = "CeilingBulb",
            Position = new Vector3(0, 4.45f, 0),
            LightColor = new Color(1.0f, 0.84f, 0.60f),
            LightEnergy = 1.15f,
            OmniRange = 16.0f,
            OmniAttenuation = 0.9f,
            ShadowEnabled = false
        };
        AddChild(light);

        var fill = new DirectionalLight3D
        {
            Name = "WeakFill",
            RotationDegrees = new Vector3(-48f, -32f, 0),
            LightColor = new Color(0.75f, 0.72f, 0.66f),
            LightEnergy = 0.35f,
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
