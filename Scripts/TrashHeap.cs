using System;
using Godot;

namespace TrashPcTycoon;

/// <summary>
/// 3D-куча мусора на свалке в углу гаража. Сама решает только визуал и анимацию
/// копания; что именно выкопано, определяют PartCatalog и Main (через сигнал она не тянет экономику).
/// Коллайдер в группе "trash_heap" — по нему Main понимает тап через рейкаст камеры.
/// </summary>
public partial class TrashHeap : Node3D
{
    private Node3D _heapVisual = null!;
    private Label3D _sign = null!;
    private int _digCount;
    private bool _animating;
    private bool _depleted;

    public const string GroupName = "trash_heap";

    public override void _Ready()
    {
        BuildVisual();
        BuildCollider();
    }

    public bool IsDepleted => _depleted;

    /// <summary>Свалка (2.0): куча выработана — сплющивается и честно об этом сообщает.</summary>
    public void SetDepleted()
    {
        _depleted = true;
        _heapVisual.Scale = new Vector3(1f, 0.22f, 1f);
        _sign.Text = "ПУСТО\nпривезут ещё";
    }

    /// <summary>Дёргает кучу, разбрасывает кубики мусора и слегка «проседает» каждые несколько копок.</summary>
    public void PlayDigEffect(Random rng)
    {
        _digCount++;
        if (_animating)
        {
            return;
        }

        _animating = true;
        float baseScale = _depleted ? 0.22f : (_digCount % 8 == 0 ? 1.0f : Mathf.Max(0.78f, 1.0f - _digCount * 0.015f));
        Tween tween = CreateTween();
        tween.TweenProperty(_heapVisual, "rotation_degrees", new Vector3(0, rng.Next(-7, 8), 0), 0.07);
        tween.TweenProperty(_heapVisual, "scale", Vector3.One * (baseScale * 0.94f), 0.07);
        tween.TweenProperty(_heapVisual, "scale", Vector3.One * baseScale, 0.12);
        tween.TweenCallback(Callable.From(() => _animating = false));

        for (int i = 0; i < 5; i++)
        {
            SpawnDebris(rng);
        }

        SpawnDustBurst();
    }

    /// <summary>Пылевой взрыв из-под лопаты (2.0/Модуль 3): коричнево и честно воняет.</summary>
    private void SpawnDustBurst()
    {
        var material = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 1, 0),
            Spread = 40f,
            InitialVelocityMin = 1.2f,
            InitialVelocityMax = 2.6f,
            Gravity = new Vector3(0, -1.6f, 0),
            ScaleMin = 0.10f,
            ScaleMax = 0.22f,
            Color = new Color(0.55f, 0.46f, 0.34f, 0.8f)
        };

        var mesh = new BoxMesh { Size = new Vector3(0.14f, 0.10f, 0.14f) };
        mesh.Material = PixelTextureFactory.Flat(new Color(0.55f, 0.46f, 0.34f, 0.8f));

        var particles = new GPUParticles3D
        {
            Amount = 18,
            Lifetime = 0.65f,
            OneShot = true,
            Explosiveness = 0.85f,
            ProcessMaterial = material,
            DrawPass1 = mesh,
            Position = new Vector3(0, 0.7f, 0)
        };
        AddChild(particles);
        particles.Emitting = true;

        Tween tween = CreateTween();
        tween.TweenInterval(1.0f);
        tween.TweenCallback(Callable.From(() => particles.QueueFree()));
    }

    private void SpawnDebris(Random rng)
    {
        MeshInstance3D debris = PartVisualFactory.Box("Debris",
            new Vector3(rng.Next(-8, 9) * 0.12f, 0.9f, rng.Next(-8, 9) * 0.12f),
            new Vector3(0.16f, 0.14f, 0.16f),
            PixelTextureFactory.Flat(new Color(0.38f + (float)rng.NextDouble() * 0.2f, 0.30f, 0.20f)));
        AddChild(debris);

        var target = new Vector3(rng.Next(-18, 19) * 0.12f, -0.4f, rng.Next(-18, 19) * 0.12f);
        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(debris, "position", target, 0.55).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(debris, "rotation_degrees",
            new Vector3(rng.Next(0, 360), rng.Next(0, 360), 0), 0.55);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => debris.QueueFree()));
    }

    private void BuildVisual()
    {
        _heapVisual = new Node3D { Name = "HeapVisual" };
        AddChild(_heapVisual);

        var rng = new Random(1234);
        StandardMaterial3D[] materials =
        {
            PixelTextureFactory.RustMetal(),
            PixelTextureFactory.DarkMetal(),
            PixelTextureFactory.Cardboard(),
            PixelTextureFactory.Flat(new Color(0.22f, 0.24f, 0.22f))
        };

        for (int i = 0; i < 20; i++)
        {
            float angle = (float)rng.NextDouble() * Mathf.Tau;
            float radius = (float)rng.NextDouble() * 1.15f;
            var pos = new Vector3(Mathf.Cos(angle) * radius, 0.18f + (float)rng.NextDouble() * 0.75f, Mathf.Sin(angle) * radius);
            var size = new Vector3(0.35f + (float)rng.NextDouble() * 0.65f,
                0.25f + (float)rng.NextDouble() * 0.45f, 0.35f + (float)rng.NextDouble() * 0.65f);
            MeshInstance3D chunk = PartVisualFactory.Box($"Chunk{i}", pos, size, materials[rng.Next(materials.Length)]);
            chunk.RotationDegrees = new Vector3(rng.Next(-18, 19), rng.Next(0, 360), rng.Next(-18, 19));
            _heapVisual.AddChild(chunk);
        }

        MeshInstance3D tire = PartVisualFactory.Cylinder("Tire", new Vector3(-1.05f, 0.42f, 0.55f),
            0.52f, 0.28f, PixelTextureFactory.Flat(new Color(0.09f, 0.09f, 0.09f)));
        tire.RotationDegrees = new Vector3(90f, 0, 24f);
        _heapVisual.AddChild(tire);

        MeshInstance3D monitor = PartVisualFactory.Box("DeadMonitor", new Vector3(0.75f, 0.95f, -0.45f),
            new Vector3(0.95f, 0.75f, 0.75f), PixelTextureFactory.DarkMetal());
        monitor.RotationDegrees = new Vector3(-8f, 32f, 6f);
        _heapVisual.AddChild(monitor);
        MeshInstance3D screen = PartVisualFactory.Box("DeadScreen", new Vector3(0.98f, 0.98f, -0.12f),
            new Vector3(0.68f, 0.48f, 0.04f), PixelTextureFactory.Flat(new Color(0.28f, 0.34f, 0.30f), 0.5f));
        screen.RotationDegrees = new Vector3(-8f, 32f, 6f);
        _heapVisual.AddChild(screen);

        _sign = new Label3D
        {
            Text = "СВАЛКА\nтапни — копай",
            PixelSize = 0.007f,
            FontSize = 44,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(0.92f, 0.85f, 0.66f),
            OutlineSize = 9,
            OutlineModulate = new Color(0.07f, 0.06f, 0.05f),
            Position = new Vector3(0, 1.95f, 0)
        };
        _heapVisual.AddChild(_sign);
    }

    private void BuildCollider()
    {
        var body = new StaticBody3D { Name = "HeapBody" };
        body.AddToGroup(GroupName);
        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(3.4f, 1.8f, 3.2f) },
            Position = new Vector3(0, 0.85f, 0)
        };
        body.AddChild(shape);
        AddChild(body);
    }
}
