using Godot;

namespace TrashPcTycoon;

/// <summary>
/// 3D-школьник из кубов: худи, кепка, рюкзак, телефон в руке. Приходит пешком
/// (tween от «двери»), покачивается в ожидании и так же пешком уходит —
/// довольный с ПК мечты или быстро, если ПК решил стать дым-машиной.
/// </summary>
public partial class ClientController : Node3D
{
    private Node3D _body = null!;
    private MeshInstance3D _hoodie = null!;
    private MeshInstance3D _cap = null!;
    private Label3D _bubble = null!;
    private float _bobTime;
    private bool _present;

    private static readonly Vector3 CounterPosition = new(3.6f, 0f, -0.4f);
    private static readonly Vector3 DoorPosition = new(6.4f, 0f, -3.4f);

    public bool IsPresent => _present;

    public override void _Ready()
    {
        BuildKid();
        Position = DoorPosition;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!_present)
        {
            return;
        }

        _bobTime += (float)delta;
        _body.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_bobTime * 2.4f)) * 0.045f, 0);
        _body.RotationDegrees = new Vector3(0, Mathf.Sin(_bobTime * 0.9f) * 5f, 0);
    }

    public void Arrive(ClientData client)
    {
        _hoodie.MaterialOverride = PixelTextureFactory.Flat(client.HoodieColor);
        _cap.MaterialOverride = PixelTextureFactory.Flat(client.CapColor);
        Visible = true;
        _present = true;
        Position = DoorPosition;
        Say(client.Name);
        Tween tween = CreateTween();
        tween.TweenProperty(this, "position", CounterPosition, 1.1).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    public void Leave(bool happy)
    {
        _present = false;
        Say(happy ? "Оно ЖИВОЕ! И тянет!" : "Я… я просто пойду. К другому дяде.");
        Vector3 exit = happy ? new Vector3(6.4f, 0f, 1.8f) : DoorPosition;
        Tween tween = CreateTween();
        tween.TweenProperty(this, "position", exit, happy ? 1.2 : 0.65)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() => Visible = false));
    }

    public void Say(string text)
    {
        _bubble.Text = text;
    }

    private void BuildKid()
    {
        _body = new Node3D { Name = "Kid" };
        AddChild(_body);

        StandardMaterial3D jeans = PixelTextureFactory.Flat(new Color(0.25f, 0.30f, 0.42f));
        StandardMaterial3D skin = PixelTextureFactory.Flat(new Color(0.85f, 0.65f, 0.50f));
        _hoodie = PartVisualFactory.Box("Hoodie", new Vector3(0, 0.95f, 0), new Vector3(0.62f, 0.72f, 0.36f),
            PixelTextureFactory.Flat(new Color(0.45f, 0.30f, 0.22f)));
        _cap = PartVisualFactory.Box("Cap", new Vector3(0, 1.58f, 0.02f), new Vector3(0.46f, 0.14f, 0.46f),
            PixelTextureFactory.Flat(new Color(0.55f, 0.25f, 0.18f)));

        _body.AddChild(PartVisualFactory.Box("LegL", new Vector3(-0.16f, 0.30f, 0), new Vector3(0.22f, 0.60f, 0.26f), jeans));
        _body.AddChild(PartVisualFactory.Box("LegR", new Vector3(0.16f, 0.30f, 0), new Vector3(0.22f, 0.60f, 0.26f), jeans));
        _body.AddChild(PartVisualFactory.Box("ShoeL", new Vector3(-0.16f, 0.04f, 0.05f), new Vector3(0.24f, 0.09f, 0.38f),
            PixelTextureFactory.Flat(new Color(0.75f, 0.72f, 0.68f))));
        _body.AddChild(PartVisualFactory.Box("ShoeR", new Vector3(0.16f, 0.04f, 0.05f), new Vector3(0.24f, 0.09f, 0.38f),
            PixelTextureFactory.Flat(new Color(0.75f, 0.72f, 0.68f))));
        _body.AddChild(_hoodie);
        _body.AddChild(PartVisualFactory.Box("ArmL", new Vector3(-0.40f, 0.92f, 0), new Vector3(0.16f, 0.58f, 0.20f),
            PixelTextureFactory.Flat(new Color(0.40f, 0.27f, 0.20f))));
        _body.AddChild(PartVisualFactory.Box("ArmR", new Vector3(0.40f, 0.92f, 0.10f), new Vector3(0.16f, 0.58f, 0.20f), skin));
        _body.AddChild(PartVisualFactory.Box("Phone", new Vector3(0.40f, 0.68f, 0.24f), new Vector3(0.16f, 0.26f, 0.04f),
            PixelTextureFactory.Flat(new Color(0.14f, 0.15f, 0.17f), 0.4f)));
        _body.AddChild(PartVisualFactory.Box("Head", new Vector3(0, 1.38f, 0), new Vector3(0.44f, 0.42f, 0.42f), skin));
        _body.AddChild(_cap);
        _body.AddChild(PartVisualFactory.Box("CapBrim", new Vector3(0, 1.52f, 0.30f), new Vector3(0.46f, 0.05f, 0.24f),
            PixelTextureFactory.Flat(new Color(0.45f, 0.20f, 0.15f))));
        _body.AddChild(PartVisualFactory.Box("Backpack", new Vector3(0, 1.00f, -0.30f), new Vector3(0.52f, 0.62f, 0.22f),
            PixelTextureFactory.Flat(new Color(0.55f, 0.45f, 0.20f))));

        _bubble = new Label3D
        {
            Text = "",
            PixelSize = 0.007f,
            FontSize = 40,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(0.95f, 0.90f, 0.76f),
            OutlineSize = 9,
            OutlineModulate = new Color(0.07f, 0.06f, 0.05f),
            Position = new Vector3(0, 2.05f, 0)
        };
        _body.AddChild(_bubble);

        RotationDegrees = new Vector3(0, -28f, 0);
    }
}
