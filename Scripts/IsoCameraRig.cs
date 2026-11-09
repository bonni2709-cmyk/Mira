using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Изометрическая камера: ортографическая проекция, классический угол подъёма
/// (~35.264°) и поворот по азимуту перетаскиванием. Никаких теней-экранов и
/// кинематографичных пролётов — гараж должен читаться как диорама с приставки.
/// </summary>
public partial class IsoCameraRig : Node3D
{
    private Camera3D _camera = null!;
    private float _yawRad = Mathf.DegToRad(45.0f);
    private float _distance = 20.0f;

    private const float ElevationRad = 0.61548f; // asin(1/sqrt(3)) — честная изометрия
    private const float MinSize = 8.0f;
    private const float MaxSize = 22.0f;

    public Camera3D Camera => _camera;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Size = 13.0f;
        _camera.Near = 0.05f;
        _camera.Far = 100.0f;
        ApplyTransform();
    }

    public void RotateYaw(float deltaRad)
    {
        _yawRad += deltaRad;
        ApplyTransform();
    }

    public void Zoom(float deltaSize)
    {
        _camera.Size = Mathf.Clamp(_camera.Size + deltaSize, MinSize, MaxSize);
    }

    private void ApplyTransform()
    {
        float cosEl = Mathf.Cos(ElevationRad);
        var offset = new Vector3(
            Mathf.Sin(_yawRad) * cosEl,
            Mathf.Sin(ElevationRad),
            Mathf.Cos(_yawRad) * cosEl) * _distance;
        _camera.Position = offset;
        _camera.LookAt(new Vector3(0, 0.6f, 0), Vector3.Up);
    }
}
