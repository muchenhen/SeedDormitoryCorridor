using System.Drawing;
using System.Drawing.Imaging;
using SeedDormitoryCorridor.Assets;
using SeedDormitoryCorridor.Rendering;
using SeedDormitoryCorridor.Runtime;

namespace SeedDormitoryCorridor.Rendering.Tests;

public sealed class LayeredWindowRendererTests
{
    [Fact]
    public void RenderTreatsDestinationRectangleAsPixelsAtDesktopDpi()
    {
        using var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppPArgb);
        bitmap.SetPixel(0, 0, Color.Red);
        bitmap.SetPixel(1, 0, Color.Green);
        bitmap.SetPixel(0, 1, Color.Blue);
        bitmap.SetPixel(1, 1, Color.White);
        using var sheet = new DecodedSpriteSheet((Bitmap)bitmap.Clone(), [255, 255, 255, 255]);
        var atlas = new AtlasDefinition(2, 2, 1, 1, 2, 2,
            new AnimationCatalog([new AnimationDefinition("idle", 0, [100])]));
        using var renderer = new LayeredWindowRenderer();
        renderer.LoadSpriteSheet(sheet, atlas);

        renderer.Render(new SpriteRenderFrame(0, 0));

        Assert.Equal(Color.Red.ToArgb(), renderer.Surface.GetPixel(0, 0).ToArgb());
        Assert.Equal(Color.White.ToArgb(), renderer.Surface.GetPixel(1, 1).ToArgb());
    }

    [Fact]
    public void HitTestUsesCurrentCellThresholdAndScaling()
    {
        using var bitmap = new Bitmap(4, 2, PixelFormat.Format32bppPArgb);
        byte[] alpha = new byte[8];
        alpha[1] = 20;
        using var sheet = new DecodedSpriteSheet((Bitmap)bitmap.Clone(), alpha);
        var atlas = new AtlasDefinition(4, 2, 2, 1, 2, 2,
            new AnimationCatalog([new AnimationDefinition("idle", 0, [100, 100])]));
        using var renderer = new LayeredWindowRenderer();
        renderer.LoadSpriteSheet(sheet, atlas);
        renderer.Resize(2f, DpiScale.Default);
        renderer.Render(new SpriteRenderFrame(0, 0));

        Assert.True(renderer.HitTest(2, 0, 16));
        Assert.False(renderer.HitTest(2, 0, 21));
        Assert.False(renderer.HitTest(-1, 0, 1));
    }

    [Fact]
    public void FlippedHitTestReversesSourceX()
    {
        using var bitmap = new Bitmap(2, 1, PixelFormat.Format32bppPArgb);
        using var sheet = new DecodedSpriteSheet((Bitmap)bitmap.Clone(), [255, 0]);
        var atlas = new AtlasDefinition(2, 1, 1, 1, 2, 1,
            new AnimationCatalog([new AnimationDefinition("idle", 0, [100])]));
        using var renderer = new LayeredWindowRenderer();
        renderer.LoadSpriteSheet(sheet, atlas);
        renderer.Render(new SpriteRenderFrame(0, 0, true));

        Assert.False(renderer.HitTest(0, 0, 16));
        Assert.True(renderer.HitTest(1, 0, 16));
    }
}
