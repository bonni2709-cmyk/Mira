using System;
using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Генерирует маленькие «пиксельные» текстуры в коде (пятна ржавчины, бетон, картон),
/// чтобы не тащить бинарные ассеты ради Модуля 1. Фильтрация — только Nearest:
/// никакого сглаживания, честный крупный пиксель, как на старой приставке.
/// </summary>
public static class PixelTextureFactory
{
    // 3.0/Модуль 2: текстуры выросли 64 -> 128, к альбедо добавились нормал-мапы из той же высоты.
    private const int Size = 128;

    public static StandardMaterial3D Concrete() =>
        MakeMaterial(BaseColor: new Color(0.42f, 0.40f, 0.37f), seed: 11, speckles: 620, cracks: 4);

    public static StandardMaterial3D Wall() =>
        MakeMaterial(BaseColor: new Color(0.46f, 0.42f, 0.34f), seed: 23, speckles: 480, cracks: 7);

    public static StandardMaterial3D RustMetal() =>
        MakeMaterial(BaseColor: new Color(0.48f, 0.36f, 0.24f), seed: 37, speckles: 1000, cracks: 2);

    public static StandardMaterial3D DarkMetal() =>
        MakeMaterial(BaseColor: new Color(0.32f, 0.33f, 0.33f), seed: 41, speckles: 580, cracks: 3);

    public static StandardMaterial3D Cardboard() =>
        MakeMaterial(BaseColor: new Color(0.62f, 0.46f, 0.27f), seed: 53, speckles: 360, cracks: 1);

    public static StandardMaterial3D Wood() =>
        MakeMaterial(BaseColor: new Color(0.45f, 0.32f, 0.18f), seed: 67, speckles: 430, cracks: 5);

    /// <summary>
    /// Пол гаража (4.0/Модуль 2): NoiseTexture2D — масло, грязь и века не мытья.
    /// Пиксельная фильтрация сохраняет стиль, шум убирает «пластиковость» бетона.
    /// </summary>
    public static StandardMaterial3D GarageFloor()
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = 0.055f,
            FractalOctaves = 4,
            FractalGain = 0.55f,
            Seed = 1234
        };

        var ramp = new Gradient();
        ramp.SetColor(0, new Color(0.14f, 0.125f, 0.105f));
        ramp.AddPoint(0.45f, new Color(0.30f, 0.27f, 0.21f));
        ramp.AddPoint(0.75f, new Color(0.26f, 0.21f, 0.16f));
        ramp.SetColor(1, new Color(0.40f, 0.35f, 0.27f));

        var texture = new NoiseTexture2D
        {
            Width = 256,
            Height = 256,
            Noise = noise,
            ColorRamp = ramp,
            Seamless = true
        };

        return new StandardMaterial3D
        {
            AlbedoTexture = texture,
            Roughness = 0.96f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Uv1Scale = new Vector3(3.0f, 3.0f, 3.0f)
        };
    }

    /// <summary>Корпус ПК: родной цвет, щедро тронутый ржавчиной (3.0/Модуль 2).</summary>
    public static StandardMaterial3D RustedTint(Color tint)
    {
        var rusty = new Color(0.48f, 0.36f, 0.24f);
        var mixed = new Color(
            tint.R * 0.5f + rusty.R * 0.5f,
            tint.G * 0.5f + rusty.G * 0.5f,
            tint.B * 0.5f + rusty.B * 0.5f);
        return MakeMaterial(BaseColor: mixed, seed: 97, speckles: 1150, cracks: 3);
    }

    public static StandardMaterial3D Flat(Color color, float roughness = 0.95f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = roughness,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
        };
    }

    private static StandardMaterial3D MakeMaterial(Color BaseColor, int seed, int speckles, int cracks)
    {
        var rng = new Random(seed);
        Image image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgb8);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float shade = 0.92f + (float)rng.NextDouble() * 0.16f;
                image.SetPixel(x, y, new Color(BaseColor.R * shade, BaseColor.G * shade, BaseColor.B * shade));
            }
        }

        for (int i = 0; i < speckles; i++)
        {
            int x = rng.Next(Size);
            int y = rng.Next(Size);
            float dark = 0.55f + (float)rng.NextDouble() * 0.25f;
            Color c = image.GetPixel(x, y);
            image.SetPixel(x, y, new Color(c.R * dark, c.G * dark, c.B * dark));
        }

        for (int i = 0; i < cracks; i++)
        {
            int x = rng.Next(Size);
            int y = rng.Next(Size);
            int length = rng.Next(6, 20);
            for (int step = 0; step < length; step++)
            {
                int px = Mathf.Clamp(x + step, 0, Size - 1);
                int py = Mathf.Clamp(y + (step % 3 == 0 ? 1 : 0), 0, Size - 1);
                Color c = image.GetPixel(px, py);
                image.SetPixel(px, py, new Color(c.R * 0.6f, c.G * 0.6f, c.B * 0.6f));
            }
        }

        ImageTexture texture = ImageTexture.CreateFromImage(image);
        return new StandardMaterial3D
        {
            AlbedoTexture = texture,
            NormalTexture = ImageTexture.CreateFromImage(BuildNormalMap(image)),
            NormalScale = 0.7f,
            Roughness = 1.0f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Uv1Scale = new Vector3(2.0f, 2.0f, 2.0f)
        };
    }

    /// <summary>
    /// Нормал-мапа из яркости той же текстуры (высота = светлота пикселя).
    /// Текстуры статичны и генерируются один раз, поэтому FPS это не трогает вообще,
    /// а ржавчина и обшарпанность начинают ловить свет лампочки объёмом.
    /// </summary>
    private static Image BuildNormalMap(Image source)
    {
        Image normal = Image.CreateEmpty(Size, Size, false, Image.Format.Rgb8);

        float HeightAt(int x, int y)
        {
            Color c = source.GetPixel(Mathf.Clamp(x, 0, Size - 1), Mathf.Clamp(y, 0, Size - 1));
            return (c.R + c.G + c.B) / 3f;
        }

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float dx = HeightAt(x - 1, y) - HeightAt(x + 1, y);
                float dy = HeightAt(x, y - 1) - HeightAt(x, y + 1);
                var n = new Vector3(dx * 2.2f, dy * 2.2f, 1f).Normalized();
                normal.SetPixel(x, y, new Color(n.X * 0.5f + 0.5f, n.Y * 0.5f + 0.5f, n.Z * 0.5f + 0.5f));
            }
        }

        return normal;
    }
}
