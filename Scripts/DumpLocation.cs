using System;
using System.Collections.Generic;
using Godot;
using TrashPcTycoon.UI;

namespace TrashPcTycoon;

/// <summary>
/// Отдельная локация «Свалка» (Обновление 2.0): своя 3D-сцена (Scenes/Dump.tscn),
/// горы мусора, бродячие собаки, грязь и лужи. Кучи истощаются от копки;
/// «Уехать и вернуться» пересоздаёт сцену с новым мусором, а раз в 90 секунд
/// одна пустая куча «подвозит» сама. Деньги/инвентарь живут в автозагрузке GameState,
/// поэтому между сценами ничего не теряется, а стол сборки Main сохранил перед выездом.
/// </summary>
public partial class DumpLocation : Node3D
{
    private readonly Random _rng = new();
    private readonly Dictionary<TrashHeap, int> _heapDigsLeft = new();
    private readonly List<Dog> _dogs = new();

    private GameState _state = null!;
    private DumpUI _ui = null!;
    private IsoCameraRig _cameraRig = null!;
    private SoundSystem _sound = null!;
    private float _refillTimer = 90f;

    private Vector2 _pressPosition;
    private bool _pointerDown;
    private bool _dragging;
    private ulong _lastTapMsec;

    private sealed class Dog
    {
        public required Node3D Root;
        public required MeshInstance3D Tail;
        public Vector3 Target;
        public float WaitTimer;
        public float WagPhase;
    }

    public override void _Ready()
    {
        BuildEnvironment();
        BuildGroundAndMountains();
        BuildDogs();

        _state = GetNode<GameState>("/root/GameState");
        _ui = GetNode<DumpUI>("UI");
        _cameraRig = GetNode<IsoCameraRig>("CameraRig");
        _sound = GetNode<SoundSystem>("SoundSystem");

        foreach (string heapName in new[] { "Heap1", "Heap2", "Heap3" })
        {
            TrashHeap heap = GetNode<TrashHeap>(heapName);
            _heapDigsLeft[heap] = _rng.Next(7, 13);
        }

        _ui.Bind(_state);
        _ui.BackToGaragePressed += () => GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
        _ui.RefreshDumpPressed += () => GetTree().ReloadCurrentScene();
        _ui.DigPressed += DigRandomHeap;
        UpdateHeapsStatus();
        _ui.SetMessage("Приехал на свалку. Собаки делают вид, что ты не интересен. Тапай по кучам!");

        GD.Print("Trash-PC Tycoon: локация «Свалка» загружена.");
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;

        _refillTimer -= d;
        if (_refillTimer <= 0f)
        {
            _refillTimer = 90f;
            RefillOneHeap();
        }

        foreach (Dog dog in _dogs)
        {
            dog.WagPhase += d * 9f;
            dog.Tail.RotationDegrees = new Vector3(0, 0, Mathf.Sin(dog.WagPhase) * 28f);

            if (dog.WaitTimer > 0f)
            {
                dog.WaitTimer -= d;
                continue;
            }

            Vector3 to = dog.Target - dog.Root.Position;
            to.Y = 0;
            if (to.Length() < 0.25f)
            {
                dog.WaitTimer = 1.5f + (float)_rng.NextDouble() * 3.5f;
                dog.Target = RandomGroundPoint();
                continue;
            }

            dog.Root.Position += to.Normalized() * d * 1.1f;
            dog.Root.RotationDegrees = new Vector3(0, Mathf.RadToDeg(Mathf.Atan2(to.X, to.Z)), 0);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse:
                if (mouse.Pressed) { _pointerDown = true; _dragging = false; _pressPosition = mouse.Position; }
                else { EndPointer(mouse.Position); }
                break;
            case InputEventMouseMotion motion when _pointerDown:
                if (!_dragging && motion.Position.DistanceTo(_pressPosition) > 24f)
                {
                    _dragging = true;
                }

                if (_dragging)
                {
                    _cameraRig.RotateYaw(-motion.Relative.X * 0.008f);
                }

                break;
            case InputEventScreenTouch touch:
                if (touch.Pressed) { _pointerDown = true; _dragging = false; _pressPosition = touch.Position; }
                else { EndPointer(touch.Position); }
                break;
            case InputEventScreenDrag drag when _pointerDown:
                if (!_dragging && drag.Position.DistanceTo(_pressPosition) > 24f)
                {
                    _dragging = true;
                }

                if (_dragging)
                {
                    _cameraRig.RotateYaw(-drag.Relative.X * 0.008f);
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                _cameraRig.Zoom(-1.0f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                _cameraRig.Zoom(1.0f);
                break;
        }
    }

    private void EndPointer(Vector2 position)
    {
        if (!_pointerDown)
        {
            return;
        }

        _pointerDown = false;
        bool wasDrag = _dragging || position.DistanceTo(_pressPosition) > 24f;
        _dragging = false;
        if (wasDrag)
        {
            return;
        }

        ulong now = Time.GetTicksMsec();
        if (now - _lastTapMsec < 90)
        {
            return;
        }

        _lastTapMsec = now;

        Camera3D camera = _cameraRig.Camera;
        Vector3 origin = camera.ProjectRayOrigin(position);
        Vector3 end = origin + camera.ProjectRayNormal(position) * 120f;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
        {
            return;
        }

        var collider = hit["collider"].AsGodotObject() as Node;
        if (collider != null && collider.IsInGroup(TrashHeap.GroupName)
            && collider.GetParent() is TrashHeap heap && _heapDigsLeft.ContainsKey(heap))
        {
            DigHeap(heap);
        }
    }

    private void DigRandomHeap()
    {
        var alive = new List<TrashHeap>();
        foreach (KeyValuePair<TrashHeap, int> pair in _heapDigsLeft)
        {
            if (pair.Value > 0)
            {
                alive.Add(pair.Key);
            }
        }

        if (alive.Count == 0)
        {
            _ui.SetMessage("Всё выкопано до грунта. Жми «Уехать и вернуться» — или подожди, мусор сам себя привезёт.");
            return;
        }

        DigHeap(alive[_rng.Next(alive.Count)]);
    }

    private void DigHeap(TrashHeap heap)
    {
        if (_state.InventoryFull)
        {
            _ui.SetMessage($"Рюкзак полон ({_state.Inventory.Count}/{_state.InventoryCapacity})! В гараже сдай лишнее барахольщику или купи ящик «Кладовка». Куча подождёт.");
            return;
        }

        if (_heapDigsLeft[heap] <= 0)
        {
            _ui.SetMessage("Эта куча пуста, как обещания продавцов. Ищи соседнюю.");
            return;
        }

        _heapDigsLeft[heap]--;
        heap.PlayDigEffect(_rng);
        _sound.PlayDig();

        if (_heapDigsLeft[heap] <= 0)
        {
            heap.SetDepleted();
        }

        int roll = _rng.Next(100);
        if (roll < 5)
        {
            PcPart epic = PartCatalog.CreateEpicFind(_rng);
            _state.AddPart(epic);
            _sound.PlayCash();
            _ui.SetMessage($"ЛЕГЕНДА СВАЛКИ: {epic.Name}! {epic.Description}");
        }
        else if (roll < 60)
        {
            PcPart part = PartCatalog.CreateDumpFind(_rng);
            _state.AddPart(part);
            _ui.SetMessage($"Выкопал: {part.Name}. {part.Description}");
        }
        else if (roll < 82)
        {
            _state.AddMoney(PartCatalog.ScrapRewardRub);
            _ui.SetMessage($"{PartCatalog.RandomScrapLine(_rng)} +{PartCatalog.ScrapRewardRub} ₽");
        }
        else
        {
            _ui.SetMessage(PartCatalog.RandomNothingLine(_rng));
        }

        SaveSystem.Save(_state, null);
        UpdateHeapsStatus();
    }

    private void RefillOneHeap()
    {
        foreach (KeyValuePair<TrashHeap, int> pair in _heapDigsLeft)
        {
            if (pair.Value <= 0)
            {
                GetTree().ReloadCurrentScene();
                return;
            }
        }
    }

    private void UpdateHeapsStatus()
    {
        int alive = 0;
        int digs = 0;
        foreach (KeyValuePair<TrashHeap, int> pair in _heapDigsLeft)
        {
            if (pair.Value > 0)
            {
                alive++;
                digs += pair.Value;
            }
        }

        _ui.SetHeapsStatus($"Кучи: {alive}/3 (копок осталось ~{digs})");
    }

    private Vector3 RandomGroundPoint()
    {
        return new Vector3((float)(_rng.NextDouble() * 14.0 - 7.0), 0, (float)(_rng.NextDouble() * 10.0 - 5.0));
    }

    private void BuildDogs()
    {
        _dogs.Add(MakeDog(new Vector3(-4f, 0, 3.5f), new Color(0.55f, 0.42f, 0.28f)));
        _dogs.Add(MakeDog(new Vector3(5f, 0, -3f), new Color(0.35f, 0.32f, 0.30f)));
    }

    private Dog MakeDog(Vector3 position, Color furColor)
    {
        var root = new Node3D { Position = position };
        StandardMaterial3D fur = PixelTextureFactory.Flat(furColor);
        StandardMaterial3D dark = PixelTextureFactory.Flat(new Color(0.14f, 0.12f, 0.11f));

        root.AddChild(PartVisualFactory.Box("Body", new Vector3(0, 0.48f, 0), new Vector3(0.85f, 0.42f, 0.38f), fur));
        root.AddChild(PartVisualFactory.Box("Head", new Vector3(0.58f, 0.68f, 0), new Vector3(0.36f, 0.32f, 0.34f), fur));
        root.AddChild(PartVisualFactory.Box("Snout", new Vector3(0.82f, 0.60f, 0), new Vector3(0.20f, 0.16f, 0.20f), dark));
        root.AddChild(PartVisualFactory.Box("EarL", new Vector3(0.48f, 0.90f, -0.10f), new Vector3(0.08f, 0.20f, 0.08f), dark));
        root.AddChild(PartVisualFactory.Box("EarR", new Vector3(0.48f, 0.90f, 0.10f), new Vector3(0.08f, 0.20f, 0.08f), dark));
        root.AddChild(PartVisualFactory.Box("LegFL", new Vector3(0.30f, 0.18f, -0.12f), new Vector3(0.10f, 0.36f, 0.10f), fur));
        root.AddChild(PartVisualFactory.Box("LegFR", new Vector3(0.30f, 0.18f, 0.12f), new Vector3(0.10f, 0.36f, 0.10f), fur));
        root.AddChild(PartVisualFactory.Box("LegBL", new Vector3(-0.30f, 0.18f, -0.12f), new Vector3(0.10f, 0.36f, 0.10f), fur));
        root.AddChild(PartVisualFactory.Box("LegBR", new Vector3(-0.30f, 0.18f, 0.12f), new Vector3(0.10f, 0.36f, 0.10f), fur));
        MeshInstance3D tail = PartVisualFactory.Box("Tail", new Vector3(-0.52f, 0.66f, 0), new Vector3(0.30f, 0.08f, 0.08f), fur);
        root.AddChild(tail);
        AddChild(root);

        return new Dog { Root = root, Tail = tail, Target = RandomGroundPoint(), WagPhase = (float)_rng.NextDouble() * 6f };
    }

    private void BuildGroundAndMountains()
    {
        var ground = PartVisualFactory.Box("MudGround", new Vector3(0, -0.16f, 0), new Vector3(26f, 0.3f, 20f),
            PixelTextureFactory.Flat(new Color(0.32f, 0.26f, 0.19f)));
        AddChild(ground);

        for (int i = 0; i < 6; i++)
        {
            float x = -9f + i * 3.6f;
            AddChild(PartVisualFactory.Box($"Puddle{i}", new Vector3(x, 0.02f, 3.5f - (i % 3) * 3.2f),
                new Vector3(2.0f, 0.03f, 1.3f), PixelTextureFactory.Flat(new Color(0.20f, 0.22f, 0.20f), 0.35f)));
        }

        var rng = new Random(42);
        for (int m = 0; m < 7; m++)
        {
            float angle = m / 7.0f * Mathf.Tau;
            var center = new Vector3(Mathf.Cos(angle) * 10.5f, 0, Mathf.Sin(angle) * 8.0f);
            for (int i = 0; i < 8; i++)
            {
                var pos = center + new Vector3((rng.NextSingle() - 0.5f) * 3f, 0.3f + rng.NextSingle() * 1.3f, (rng.NextSingle() - 0.5f) * 3f);
                var size = new Vector3(0.8f + rng.NextSingle() * 1.4f, 0.5f + rng.NextSingle() * 0.8f, 0.8f + rng.NextSingle() * 1.4f);
                MeshInstance3D chunk = PartVisualFactory.Box($"Mountain{m}_{i}", pos, size,
                    rng.Next(2) == 0 ? PixelTextureFactory.RustMetal() : PixelTextureFactory.DarkMetal());
                chunk.RotationDegrees = new Vector3(0, rng.Next(360), 0);
                AddChild(chunk);
            }
        }

        AddChild(PartVisualFactory.Box("DeadCarBody", new Vector3(-7.6f, 0.55f, -4.6f), new Vector3(3.4f, 0.8f, 1.6f),
            PixelTextureFactory.RustMetal()));
        AddChild(PartVisualFactory.Cylinder("DeadCarWheel", new Vector3(-8.6f, 0.35f, -3.7f), 0.35f, 0.2f,
            PixelTextureFactory.Flat(new Color(0.10f, 0.10f, 0.10f))));
        AddChild(PartVisualFactory.Cylinder("DeadCarWheel2", new Vector3(-6.6f, 0.35f, -3.7f), 0.35f, 0.2f,
            PixelTextureFactory.Flat(new Color(0.10f, 0.10f, 0.10f))));

        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50f, -30f, 0),
            LightColor = new Color(0.85f, 0.78f, 0.66f),
            LightEnergy = 0.8f,
            ShadowEnabled = false
        };
        AddChild(sun);

        var sign = new Label3D
        {
            Text = "СВАЛКА «ВТОРАЯ ЖИЗНЬ»\nсторож: собаки. зарплата: объедки",
            PixelSize = 0.01f,
            FontSize = 44,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(0.90f, 0.84f, 0.66f),
            OutlineSize = 9,
            OutlineModulate = new Color(0.07f, 0.06f, 0.05f),
            Position = new Vector3(0, 3.4f, -7.6f)
        };
        AddChild(sign);
    }

    private void BuildEnvironment()
    {
        var environment = new Godot.Environment
        {
            BackgroundColor = new Color(0.45f, 0.40f, 0.33f),
            AmbientLightColor = new Color(0.60f, 0.55f, 0.45f),
            AmbientLightEnergy = 0.7f,
            GlowEnabled = false,
            FogEnabled = false
        };
        environment.Set("background_mode", 1);
        environment.Set("ambient_light_source", 2);
        AddChild(new WorldEnvironment { Environment = environment });
    }
}
