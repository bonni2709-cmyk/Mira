using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Переливает эмиссию RGB-ленты по радуге (2.0/Модуль 2). Это не «неоновый стиль» игры,
/// а буквально игрушечные светодиоды, от которых мажоры теряют волю и деньги.
/// </summary>
public partial class RgbGlow : Node
{
    private StandardMaterial3D? _material;
    private float _hue = 0.85f;

    public void Bind(StandardMaterial3D material)
    {
        _material = material;
    }

    public override void _Process(double delta)
    {
        if (_material == null)
        {
            return;
        }

        _hue = (_hue + (float)delta * 0.16f) % 1.0f;
        _material.Emission = Color.FromHsv(_hue, 0.75f, 1.0f);
    }
}
