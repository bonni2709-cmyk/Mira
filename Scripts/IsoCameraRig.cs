using System;
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
    private readonly Random _shakeRng = new();
    private float _yawRad = Mathf.DegToRad(45.0f);
    private float _distance = 20.0f;
    private float _shakeStrength;

    private const float ElevationRad = 0.61548f; // asin(1/sqrt(3)) — честная изометрия
    private const float MinSize = 8.0f;
    private const float MaxSize = 22.0f;

    public Camera3D Camera => _camera;

    public override void _Ready()
    {
        // 3.0/Модуль 1: камеры может не быть в сцене (так свалка уходила в чёрный экран) —
        // создаём сами, чтобы ни одна локация больше не зависела от ручной сцены.
        _camera = GetNodeOrNull<Camera3D>("Camera3D") ?? new Camera3D { Name = "Camera3D" };
        if (_camera.GetParent() == null)
        {
            AddChild(_camera);
        }

        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        // 4.0/Модуль 2: лёгкий Depth of Field — стол в фокусе, задняя стена мягче.
        // На мобильном compatibility движок атрибут пропустит; на Forward+ заработает сам.
        _camera.Attributes = new CameraAttributesPractical
        {
            DofBlurFarEnabled = true,
            DofBlurFarBegin = 16.0f,
            DofBlurFarTransition = 9.0f,
            DofBlurNearEnabled = false,
            DofBlurAmount = 0.07f
        };
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

    /// <summary>Screen shake (2.0/Модуль 3): взрыв — сильно, молоток — чуть-чуть.</summary>
    public void AddShake(float strength)
    {
        _shakeStrength = Mathf.Max(_shakeStrength, strength);
    }

    public override void _Process(double delta)
    {
        if (_shakeStrength <= 0.002f)
        {
            return;
        }

        _shakeStrength = Mathf.Max(0f, _shakeStrength - (float)delta * 1.9f);
        ApplyTransform();
        if (_shakeStrength <= 0.002f)
        {
            // Тряска кончилась — камера вернулась в честную изометрию.
            return;
        }
        _camera.Position += new Vector3(
            (float)(_shakeRng.NextDouble() * 2 - 1),
            (float)(_shakeRng.NextDouble() * 2 - 1) * 0.7f,
            (float)(_shakeRng.NextDouble() * 2 - 1)) * _shakeStrength * 0.45f;
        _camera.RotationDegrees += new Vector3(0, 0, (float)(_shakeRng.NextDouble() * 2 - 1) * _shakeStrength * 1.4f);
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
