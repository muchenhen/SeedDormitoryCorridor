namespace SeedDormitoryCorridor.Rendering;

public readonly record struct DpiScale(float X, float Y)
{
    public static DpiScale Default => new(1f, 1f);
}

public readonly record struct SpriteRenderFrame(int Column, int Row, bool FlipHorizontally = false);

public enum SpriteScalingMode
{
    Smooth,
    Pixelated,
}
