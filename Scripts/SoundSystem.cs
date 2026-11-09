using System;
using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Все звуки синтезируются в коде в PCM: скрип, стук молотка, вой вентилятора,
/// грохот «взрыва» и кассовый дзинь. Никаких внешних аудиофайлов и чужих сэмплов.
/// </summary>
public partial class SoundSystem : Node
{
    private readonly Random _rng = new();
    private AudioStreamPlayer[] _players = Array.Empty<AudioStreamPlayer>();
    private AudioStreamPlayer _fanPlayer = null!;
    private AudioStreamPlayer _blowPlayer = null!;
    private int _nextPlayer;

    private const int MixRate = 22050;

    public override void _Ready()
    {
        _players = new AudioStreamPlayer[6];
        for (int i = 0; i < _players.Length; i++)
        {
            _players[i] = new AudioStreamPlayer { Bus = "Master" };
            AddChild(_players[i]);
        }

        _fanPlayer = new AudioStreamPlayer { Bus = "Master", VolumeDb = -16.0f };
        AddChild(_fanPlayer);
        _blowPlayer = new AudioStreamPlayer { Bus = "Master", VolumeDb = -14.0f };
        AddChild(_blowPlayer);
    }

    public void PlayDig() => Play(MakeStream(SynthNoiseSweep(0.22f, 0.35f, decay: 5f)), -10f);
    public void PlayHammer() => Play(MakeStream(SynthThud()), -4f);
    public void PlayPop() => Play(MakeStream(SynthThud(140f, 0.12f)), -8f);
    public void PlayCreak() => Play(MakeStream(SynthCreak()), -12f);
    public void PlayExplosion() => Play(MakeStream(SynthExplosion()), -2f);
    public void PlayCash() => Play(MakeStream(SynthCash()), -8f);
    public void PlayFail() => Play(MakeStream(SynthSweep(300f, 60f, 0.5f)), -10f);

    /// <summary>Вскрытие коробки: хруст картона и рвущийся скотч.</summary>
    public void PlayBoxOpen() => Play(MakeStream(SynthBoxOpen()), -7f);

    /// <summary>Короткий скрип-стук жёсткого диска, который очень старается.</summary>
    public void PlayHddScratch() => Play(MakeStream(SynthHddScratch()), -13f);

    /// <summary>Продувка: шипение сжатого воздуха, пока игрок дует или держит кнопку.</summary>
    public void SetBlowing(bool on)
    {
        if (on)
        {
            if (!_blowPlayer.Playing)
            {
                _blowPlayer.Stream = MakeStream(SynthBlowLoop(), loop: true);
                _blowPlayer.Play();
            }
        }
        else if (_blowPlayer.Playing)
        {
            _blowPlayer.Stop();
        }
    }

    /// <summary>Гул вентилятора растёт с температурой: выше градус — истеричнее вой.</summary>
    public void SetFanPitch(float celsius)
    {
        _fanPlayer.PitchScale = Mathf.Clamp(0.9f + (celsius - 34f) / 220f, 0.9f, 1.35f);
    }

    public void SetFan(bool on)
    {
        if (on)
        {
            _fanPlayer.Stream = SynthFanLoop();
            _fanPlayer.Play();
        }
        else if (_fanPlayer.Playing)
        {
            _fanPlayer.Stop();
        }
    }

    private void Play(AudioStreamWav stream, float volumeDb)
    {
        AudioStreamPlayer player = _players[_nextPlayer];
        _nextPlayer = (_nextPlayer + 1) % _players.Length;
        player.Stream = stream;
        player.VolumeDb = volumeDb;
        player.PitchScale = 0.92f + (float)_rng.NextDouble() * 0.16f;
        player.Play();
    }

    // ---------- Синтез ----------

    private AudioStreamWav MakeStream(float[] samples, bool loop = false)
    {
        var data = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            data[i * 2] = (byte)(value & 0xFF);
            data[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = MixRate,
            Stereo = false,
            Data = data,
            LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = 0,
            LoopEnd = samples.Length
        };
    }

    private float[] SynthThud(float freq = 95f, float seconds = 0.18f)
    {
        int count = (int)(MixRate * seconds);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)MixRate;
            float env = Mathf.Exp(-t * 26f);
            samples[i] = Mathf.Sin(Mathf.Tau * freq * t) * env * 0.9f;
        }

        return samples;
    }

    private float[] SynthCreak()
    {
        int count = (int)(MixRate * 0.45f);
        var samples = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float freq = Mathf.Lerp(160f, 55f, t) + Mathf.Sin(t * 40f) * 18f;
            phase += freq / MixRate;
            float env = Mathf.Sin(t * Mathf.Pi);
            samples[i] = Mathf.Sin(Mathf.Tau * phase) * env * 0.5f;
        }

        return samples;
    }

    private float[] SynthSweep(float from, float to, float seconds)
    {
        int count = (int)(MixRate * seconds);
        var samples = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            phase += Mathf.Lerp(from, to, t) / MixRate;
            samples[i] = Mathf.Sin(Mathf.Tau * phase) * (1f - t) * 0.6f;
        }

        return samples;
    }

    private float[] SynthNoiseSweep(float seconds, float volume, float decay)
    {
        int count = (int)(MixRate * seconds);
        var samples = new float[count];
        float last = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)MixRate;
            float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
            last = last * 0.72f + noise * 0.28f;
            samples[i] = last * Mathf.Exp(-t * decay) * volume * 2.2f;
        }

        return samples;
    }

    private float[] SynthExplosion()
    {
        int count = (int)(MixRate * 0.85f);
        var samples = new float[count];
        float last = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)MixRate;
            float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
            last = last * 0.82f + noise * 0.18f;
            float env = Mathf.Exp(-t * 5.5f);
            float boom = Mathf.Sin(Mathf.Tau * 52f * t) * env * 0.8f;
            samples[i] = (last * 1.6f + boom) * env;
        }

        return samples;
    }

    private float[] SynthCash()
    {
        int count = (int)(MixRate * 0.35f);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)MixRate;
            float freq = t < 0.16f ? 880f : 1320f;
            float env = Mathf.Exp(-Mathf.Max(0f, t - (t < 0.16f ? 0f : 0.16f)) * 9f);
            samples[i] = Mathf.Sign(Mathf.Sin(Mathf.Tau * freq * t)) * env * 0.22f;
        }

        return samples;
    }

    private float[] SynthBoxOpen()
    {
        int count = (int)(MixRate * 0.5f);
        var samples = new float[count];
        float last = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
            last = last * 0.55f + noise * 0.45f;
            // Два рывка скотча: в начале и на трети.
            float burst = (t < 0.25f || (t > 0.38f && t < 0.55f)) ? 1.0f : 0.35f;
            float env = Mathf.Sin(Mathf.Min(1f, t * 1.15f) * Mathf.Pi);
            samples[i] = last * env * burst * 0.8f;
        }

        return samples;
    }

    private float[] SynthHddScratch()
    {
        int count = (int)(MixRate * 0.16f);
        var samples = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float freq = 1900f + Mathf.Sin(t * 55f) * 700f;
            phase += freq / MixRate;
            float click = t > 0.55f && t < 0.62f ? 0.7f : 0.0f;
            samples[i] = (Mathf.Sin(Mathf.Tau * phase) * 0.18f + click * (float)(_rng.NextDouble() * 2 - 1)) * (1f - t);
        }

        return samples;
    }

    private float[] SynthBlowLoop()
    {
        int count = MixRate;
        var samples = new float[count];
        float last = 0f;
        for (int i = 0; i < count; i++)
        {
            float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
            last = last * 0.35f + noise * 0.65f;
            samples[i] = last * 0.55f;
        }

        return samples;
    }

    private AudioStreamWav SynthFanLoop()
    {
        int count = MixRate * 2;
        var samples = new float[count];
        float last = 0f;
        for (int i = 0; i < count; i++)
        {
            float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
            last = last * 0.94f + noise * 0.06f;
            float blade = Mathf.Sin(Mathf.Tau * 9f * i / MixRate) * 0.05f;
            samples[i] = last * 1.5f + blade;
        }

        return MakeStream(samples, loop: true);
    }
}
