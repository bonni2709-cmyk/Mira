using System;
using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Датчики телефона: микрофон для продувки пыли и акселерометр для проверки
/// «а не отвалилось ли там что-то». На десктопе микрофон/акселерометр могут
/// отсутствовать — тогда игра честно пишет об этом, а продувку заменяет
/// зажатая кнопка в панели сборки (см. MainUI/Main).
/// </summary>
public partial class AndroidSensors : Node
{
    public event Action<float>? ShakeDetected;
    public event Action<bool>? BlowStateChanged;

    private AudioEffectCapture? _capture;
    private AudioStreamPlayer? _micPlayer;
    private Vector3 _previousAccel;
    private float _shakeCooldown;
    private float _blowHoldTime;

    public float Loudness { get; private set; }
    public bool IsBlowing { get; private set; }
    public bool MicrophoneActive { get; private set; }
    public string Status { get; private set; } = "Датчики ещё спят.";

    private const float BlowThreshold = 0.16f;
    private const float ShakeDeltaThreshold = 6.5f;

    public override void _Ready()
    {
        _previousAccel = Input.GetAccelerometer();
        StartMicrophone();
    }

    public void StartMicrophone()
    {
        if (MicrophoneActive)
        {
            return;
        }

        if (OS.HasFeature("android"))
        {
            OS.RequestPermission("android.permission.RECORD_AUDIO");
        }

        try
        {
            int busIndex = AudioServer.BusCount;
            AudioServer.AddBus(busIndex);
            AudioServer.SetBusName(busIndex, "MicCapture");
            AudioServer.SetBusMute(busIndex, true);

            _capture = new AudioEffectCapture { BufferLength = 0.2f };
            AudioServer.AddBusEffect(busIndex, _capture);

            _micPlayer = new AudioStreamPlayer
            {
                Stream = new AudioStreamMicrophone(),
                Bus = "MicCapture"
            };
            AddChild(_micPlayer);
            _micPlayer.Play();
            MicrophoneActive = _micPlayer.Playing;
            Status = MicrophoneActive
                ? "Микрофон слушает. Дуй в телефон, как в картридж из детства."
                : "Микрофон не завёлся. Продувай кнопкой «Дуть» в панели сборки.";
        }
        catch (Exception ex)
        {
            MicrophoneActive = false;
            Status = "Микрофон недоступен. Продувай кнопкой «Дуть» в панели сборки.";
            GD.PushWarning($"AndroidSensors: микрофон недоступен ({ex.Message}).");
        }
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;
        UpdateMicrophone(d);
        UpdateAccelerometer(d);
    }

    private void UpdateMicrophone(float delta)
    {
        if (!MicrophoneActive || _capture == null)
        {
            Loudness = Mathf.Max(0f, Loudness - delta * 3f);
            SetBlowing(false);
            return;
        }

        int frames = _capture.GetFramesAvailable();
        if (frames <= 0)
        {
            return;
        }

        var buffer = _capture.GetBuffer(frames);
        float peak = 0f;
        foreach (Vector2 frame in buffer)
        {
            peak = Mathf.Max(peak, Mathf.Max(Mathf.Abs(frame.X), Mathf.Abs(frame.Y)));
        }

        _capture.ClearBuffer();
        float target = Mathf.Clamp(peak * 1.6f, 0f, 1f);
        Loudness = Mathf.Lerp(Loudness, target, 0.45f);

        if (Loudness >= BlowThreshold)
        {
            _blowHoldTime += delta;
            if (_blowHoldTime >= 0.12f)
            {
                SetBlowing(true);
            }
        }
        else
        {
            _blowHoldTime = 0f;
            SetBlowing(false);
        }
    }

    private void UpdateAccelerometer(float delta)
    {
        _shakeCooldown = Mathf.Max(0f, _shakeCooldown - delta);
        Vector3 accel = Input.GetAccelerometer();
        if (accel.Length() < 0.01f)
        {
            return;
        }

        float jerk = (accel - _previousAccel).Length();
        _previousAccel = accel;

        if (jerk >= ShakeDeltaThreshold && _shakeCooldown <= 0f)
        {
            _shakeCooldown = 0.9f;
            ShakeDetected?.Invoke(jerk);
        }
    }

    private void SetBlowing(bool blowing)
    {
        if (IsBlowing == blowing)
        {
            return;
        }

        IsBlowing = blowing;
        BlowStateChanged?.Invoke(blowing);
    }
}
