using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Лёгкая виньетка поверх 3D (3.0/Модуль 2): края кадра уходят в темноту,
/// центр — под лампочкой. Шейдер строкой в коде, без внешних файлов,
/// мышь и касания проходят сквозь. Слой 0 — под UI (у интерфейса слой 1).
/// </summary>
public static class VignetteOverlay
{
    private const string ShaderCode = """
        shader_type canvas_item;
        render_mode unshaded;

        void fragment() {
            vec2 centered = UV - vec2(0.5, 0.5);
            float dist = length(centered) * 1.42;
            float alpha = smoothstep(0.52, 1.08, dist) * 0.44;
            COLOR = vec4(vec3(0.0, 0.0, 0.0), alpha);
        }
        """;

    public static CanvasLayer Create()
    {
        var shader = new Shader { Code = ShaderCode };
        var rect = new ColorRect
        {
            Material = new ShaderMaterial { Shader = shader },
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var layer = new CanvasLayer { Layer = 0, Name = "Vignette" };
        layer.AddChild(rect);
        return layer;
    }
}
