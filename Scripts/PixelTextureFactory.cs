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
    private const int Size = 64;

    public static StandardMaterial3D Concrete() =>
        MakeMaterial(BaseColor: new Color(0.42f, 0.40f, 0.37f), seed: 11, speckles: 260, cracks: 4);

    public static StandardMaterial3D Wall() =>
        MakeMaterial(BaseColor: new Color(0.46f, 0.42f, 0.34f), seed: 23, speckles: 200, cracks: 7);

    public static StandardMaterial3D RustMetal() =>
        MakeMaterial(BaseColor: new Color(0.48f, 0.36f, 0.24f), seed: 37, speckles: 420, cracks: 2);

    public static StandardMaterial3D DarkMetal() =>
        MakeMaterial(BaseColor: new Color(0.32f, 0.33f, 0.33f), seed: 41, speckles: 240, cracks: 3);

    public static StandardMaterial3D Cardboard() =>
        MakeMaterial(BaseColor: new Color(0.62f, 0.46f, 0.27f), seed: 53, speckles: 150, cracks: 1);

    public static StandardMaterial3D Wood() =>
        MakeMaterial(BaseColor: new Color(0.45f, 0.32f, 0.18f), seed: 67, speckles: 180, cracks: 5);

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
            Roughness = 1.0f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Uv1Scale = new Vector3(2.0f, 2.0f, 2.0f)
        };
    }
}
