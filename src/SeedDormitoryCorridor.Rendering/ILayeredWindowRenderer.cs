using System.Drawing;
using SeedDormitoryCorridor.Assets;

namespace SeedDormitoryCorridor.Rendering;

public interface ILayeredWindowRenderer : IDisposable
{
    Size LogicalFrameSize { get; }

    Size PixelSize { get; }

    Bitmap Surface { get; }

    SpriteRenderFrame CurrentFrame { get; }

    void LoadSpriteSheet(DecodedSpriteSheet spriteSheet, AtlasDefinition atlas);

    void Resize(float scale, DpiScale dpi);

    void Render(in SpriteRenderFrame frame);

    byte GetSourceAlpha(int frameColumn, int frameRow, int sourceX, int sourceY);

    bool HitTest(int clientX, int clientY, byte threshold);
}
