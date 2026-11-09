using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Собирает 3D-представление детали из примитивов: перекрашенный куб — база жанра,
/// цилиндр изображает вентилятор, маленькая коробочка — разъёмы и «начинку».
/// Все материалы — с Nearest-фильтрацией (см. PixelTextureFactory.Flat).
/// </summary>
public static class PartVisualFactory
{
    public static Node3D Create(PcPart part)
    {
        var root = new Node3D { Name = "PartVisual" };
        StandardMaterial3D body = PixelTextureFactory.Flat(part.Tint);
        StandardMaterial3D dark = PixelTextureFactory.Flat(new Color(0.16f, 0.15f, 0.14f));

        Vector3 size = part.Type switch
        {
            PartType.Gpu => new Vector3(1.25f, 0.16f, 0.62f),
            PartType.Motherboard => new Vector3(1.15f, 0.10f, 1.05f),
            PartType.Cpu => new Vector3(0.52f, 0.12f, 0.52f),
            PartType.Ram => new Vector3(1.05f, 0.08f, 0.28f),
            PartType.Psu => new Vector3(0.78f, 0.55f, 0.70f),
            PartType.Hdd => new Vector3(0.72f, 0.22f, 0.95f),
            PartType.Ssd => new Vector3(0.62f, 0.12f, 0.85f),
            PartType.ComputerCase => new Vector3(0.85f, 1.45f, 1.55f),
            PartType.Cooler => new Vector3(0.68f, 0.45f, 0.68f),
            _ => new Vector3(0.65f, 0.40f, 0.65f)
        };

        root.AddChild(Box("Body", new Vector3(0, size.Y * 0.5f, 0), size, body));

        if (part.Type is PartType.Gpu or PartType.Cooler or PartType.Psu)
        {
            root.AddChild(Cylinder("Fan", new Vector3(0, size.Y + 0.07f, 0),
                radius: 0.26f, height: 0.12f, dark));
            root.AddChild(Box("FanHub", new Vector3(0, size.Y + 0.15f, 0),
                new Vector3(0.16f, 0.05f, 0.16f), body));
        }
        else if (part.Type == PartType.Cpu)
        {
            root.AddChild(Box("HeatSpreader", new Vector3(0, size.Y + 0.03f, 0),
                new Vector3(0.36f, 0.05f, 0.36f), PixelTextureFactory.Flat(new Color(0.75f, 0.72f, 0.65f), 0.6f)));
        }
        else if (part.Type == PartType.ComputerCase)
        {
            root.AddChild(Box("SidePanelLine", new Vector3(0.44f, 0.85f, 0),
                new Vector3(0.03f, 0.9f, 1.25f), dark));
            root.AddChild(Cylinder("PowerButton", new Vector3(0.44f, 1.15f, 0.45f),
                radius: 0.08f, height: 0.05f, PixelTextureFactory.Flat(new Color(0.55f, 0.20f, 0.14f))));
        }
        else
        {
            root.AddChild(Box("Detail", new Vector3(0.12f, size.Y + 0.04f, 0.08f),
                new Vector3(size.X * 0.45f, 0.07f, size.Z * 0.45f), dark));
        }

        var label = new Label3D
        {
            Text = part.Name,
            PixelSize = 0.006f,
            FontSize = 42,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false,
            Modulate = new Color(0.92f, 0.88f, 0.78f),
            OutlineSize = 8,
            OutlineModulate = new Color(0.08f, 0.07f, 0.06f),
            Position = new Vector3(0, size.Y + 0.55f, 0)
        };
        root.AddChild(label);
        return root;
    }

    public static MeshInstance3D Box(string name, Vector3 position, Vector3 size, Material material)
    {
        var mesh = new BoxMesh { Size = size };
        return new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = position,
            MaterialOverride = material
        };
    }

    public static MeshInstance3D Cylinder(string name, Vector3 position, float radius, float height, Material material)
    {
        var mesh = new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = radius,
            Height = height,
            RadialSegments = 12
        };
        return new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = position,
            MaterialOverride = material
        };
    }
}
