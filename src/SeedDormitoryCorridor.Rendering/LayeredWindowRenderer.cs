using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SeedDormitoryCorridor.Assets;

namespace SeedDormitoryCorridor.Rendering;

/// <summary>Renders atlas frames into one reusable premultiplied back buffer.</summary>
public sealed class LayeredWindowRenderer : ILayeredWindowRenderer
{
    private DecodedSpriteSheet? spriteSheet;
    private AtlasDefinition? atlas;
    private Bitmap surface = new(1, 1, PixelFormat.Format32bppPArgb);
    private Graphics? graphics;
    private bool disposed;
    private float scale = 1f;
    private DpiScale dpi = DpiScale.Default;

    public Size LogicalFrameSize => atlas is null ? Size.Empty : new Size(atlas.FrameWidth, atlas.FrameHeight);

    public Size PixelSize => surface.Size;

    public Bitmap Surface => surface;

    public SpriteRenderFrame CurrentFrame { get; private set; }

    public SpriteScalingMode ScalingMode { get; set; } = SpriteScalingMode.Smooth;

    public void LoadSpriteSheet(DecodedSpriteSheet spriteSheet, AtlasDefinition atlas)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        this.spriteSheet = spriteSheet ?? throw new ArgumentNullException(nameof(spriteSheet));
        this.atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        Resize(scale, dpi);
    }

    public void Resize(float scale, DpiScale dpi)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (scale is < 0.25f or > 4f || dpi.X <= 0 || dpi.Y <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be 0.25–4.0 and DPI must be positive.");
        }

        this.scale = scale;
        this.dpi = dpi;
        Size logical = LogicalFrameSize;
        int width = Math.Max(1, (int)Math.Round(logical.Width * scale * dpi.X));
        int height = Math.Max(1, (int)Math.Round(logical.Height * scale * dpi.Y));
        if (surface.Width == width && surface.Height == height)
        {
            return;
        }

        graphics?.Dispose();
        surface.Dispose();
        surface = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        graphics = Graphics.FromImage(surface);
        ConfigureGraphics(graphics);
    }

    public void Render(in SpriteRenderFrame frame)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (spriteSheet is null || atlas is null)
        {
            throw new InvalidOperationException("A spritesheet must be loaded before rendering.");
        }

        if (frame.Column < 0 || frame.Column >= atlas.Columns || frame.Row < 0 || frame.Row >= atlas.Rows)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }

        graphics ??= Graphics.FromImage(surface);
        ConfigureGraphics(graphics);
        graphics.Clear(Color.Transparent);
        Rectangle source = new(frame.Column * atlas.FrameWidth, frame.Row * atlas.FrameHeight, atlas.FrameWidth, atlas.FrameHeight);
        Rectangle destination = frame.FlipHorizontally
            ? new Rectangle(surface.Width, 0, -surface.Width, surface.Height)
            : new Rectangle(0, 0, surface.Width, surface.Height);
        graphics.DrawImage(spriteSheet.Bitmap, destination, source, GraphicsUnit.Pixel);
        graphics.Flush(FlushIntention.Sync);
        CurrentFrame = frame;
    }

    public byte GetSourceAlpha(int frameColumn, int frameRow, int sourceX, int sourceY)
    {
        if (spriteSheet is null || atlas is null || frameColumn < 0 || frameRow < 0 ||
            frameColumn >= atlas.Columns || frameRow >= atlas.Rows || sourceX < 0 || sourceY < 0 ||
            sourceX >= atlas.FrameWidth || sourceY >= atlas.FrameHeight)
        {
            return 0;
        }

        return spriteSheet.GetAlpha((frameColumn * atlas.FrameWidth) + sourceX, (frameRow * atlas.FrameHeight) + sourceY);
    }

    public bool HitTest(int clientX, int clientY, byte threshold)
    {
        if (atlas is null || !HitTestMapper.TryMapToSource(clientX, clientY, surface.Width, surface.Height,
                atlas.FrameWidth, atlas.FrameHeight, CurrentFrame.FlipHorizontally, out int sourceX, out int sourceY))
        {
            return false;
        }

        return GetSourceAlpha(CurrentFrame.Column, CurrentFrame.Row, sourceX, sourceY) >= threshold;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        graphics?.Dispose();
        surface.Dispose();
        disposed = true;
    }

    private void ConfigureGraphics(Graphics target)
    {
        target.CompositingMode = CompositingMode.SourceCopy;
        target.CompositingQuality = CompositingQuality.HighSpeed;
        target.PixelOffsetMode = ScalingMode == SpriteScalingMode.Pixelated ? PixelOffsetMode.Half : PixelOffsetMode.HighQuality;
        target.InterpolationMode = ScalingMode == SpriteScalingMode.Pixelated
            ? InterpolationMode.NearestNeighbor
            : InterpolationMode.HighQualityBicubic;
        target.SmoothingMode = SmoothingMode.None;
    }
}
