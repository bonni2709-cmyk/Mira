using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Картонная коробка AliExpress на верстаке. Появляется после «доставки»,
/// вскрывается тапом по 3D-коробке (группа "delivery_box" для рейкаста в Main)
/// или кнопкой в магазине. Начинку выбирает не она, а Main через PartCatalog.
/// </summary>
public partial class DeliveryBox : Node3D
{
    private Node3D _boxRoot = null!;
    private MeshInstance3D _lidLeft = null!;
    private MeshInstance3D _lidRight = null!;
    private Node3D? _shownPart;

    public const string GroupName = "delivery_box";
    public bool HasUnopenedBox { get; private set; }

    public override void _Ready()
    {
        BuildBox();
        BuildCollider();
        Visible = false;
    }

    public void SpawnBox()
    {
        ClearShownPart();
        Visible = true;
        HasUnopenedBox = true;
        _lidLeft.RotationDegrees = Vector3.Zero;
        _lidRight.RotationDegrees = Vector3.Zero;
        _boxRoot.Scale = Vector3.One * 0.05f;
        Tween tween = CreateTween();
        tween.TweenProperty(_boxRoot, "scale", Vector3.One * 1.06f, 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_boxRoot, "scale", Vector3.One, 0.08);
    }

    /// <summary>Открывает коробку и показывает деталь над верстаком. Возвращает false, если коробки нет.</summary>
    public bool OpenBox(PcPart part)
    {
        if (!HasUnopenedBox)
        {
            return false;
        }

        HasUnopenedBox = false;
        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_lidLeft, "rotation_degrees", new Vector3(0, 0, -62f), 0.35);
        tween.TweenProperty(_lidRight, "rotation_degrees", new Vector3(0, 0, 62f), 0.35);

        _shownPart = PartVisualFactory.Create(part);
        _shownPart.Position = new Vector3(0, 0.55f, 0);
        _shownPart.Scale = Vector3.One * 0.85f;
        AddChild(_shownPart);

        Tween rise = CreateTween();
        rise.TweenProperty(_shownPart, "position", new Vector3(0, 1.25f, 0), 0.45)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        rise.TweenInterval(2.2);
        rise.TweenCallback(Callable.From(() =>
        {
            ClearShownPart();
            Visible = false;
        }));
        return true;
    }

    private void ClearShownPart()
    {
        if (_shownPart != null && IsInstanceValid(_shownPart))
        {
            _shownPart.QueueFree();
        }

        _shownPart = null;
    }

    private void BuildBox()
    {
        _boxRoot = new Node3D { Name = "BoxRoot" };
        AddChild(_boxRoot);

        StandardMaterial3D cardboard = PixelTextureFactory.Cardboard();
        StandardMaterial3D tape = PixelTextureFactory.Flat(new Color(0.78f, 0.68f, 0.38f));
        _boxRoot.AddChild(PartVisualFactory.Box("BoxBody", new Vector3(0, 0.34f, 0),
            new Vector3(1.15f, 0.68f, 0.9f), cardboard));
        _boxRoot.AddChild(PartVisualFactory.Box("Tape", new Vector3(0, 0.70f, 0),
            new Vector3(0.18f, 0.03f, 0.92f), tape));

        _lidLeft = PartVisualFactory.Box("LidLeft", new Vector3(-0.29f, 0.72f, 0),
            new Vector3(0.58f, 0.05f, 0.9f), cardboard);
        _lidRight = PartVisualFactory.Box("LidRight", new Vector3(0.29f, 0.72f, 0),
            new Vector3(0.58f, 0.05f, 0.9f), cardboard);
        _boxRoot.AddChild(_lidLeft);
        _boxRoot.AddChild(_lidRight);

        var label = new Label3D
        {
            Text = "AliExpress\nне трясти. уже поздно",
            PixelSize = 0.005f,
            FontSize = 38,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(0.90f, 0.84f, 0.68f),
            OutlineSize = 8,
            OutlineModulate = new Color(0.07f, 0.06f, 0.05f),
            Position = new Vector3(0, 1.15f, 0)
        };
        _boxRoot.AddChild(label);
    }

    private void BuildCollider()
    {
        var body = new StaticBody3D { Name = "BoxBodyCollider" };
        body.AddToGroup(GroupName);
        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.5f, 1.3f, 1.2f) },
            Position = new Vector3(0, 0.55f, 0)
        };
        body.AddChild(shape);
        AddChild(body);
    }
}
