using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SeedDormitoryCorridor.Assets;

/// <summary>Owns one decoded premultiplied spritesheet and its CPU-side alpha plane.</summary>
public sealed class DecodedSpriteSheet : IDisposable
{
    public DecodedSpriteSheet(Bitmap bitmap, byte[] alpha)
    {
        Bitmap = bitmap;
        Alpha = alpha;
    }

    public Bitmap Bitmap { get; }

    public byte[] Alpha { get; }

    public int Width => Bitmap.Width;

    public int Height => Bitmap.Height;

    public static DecodedSpriteSheet DecodePng(string path)
    {
        using var source = new Bitmap(path);
        if (source.RawFormat.Guid != ImageFormat.Png.Guid)
        {
            throw new InvalidDataException("当前版本只支持 PNG。 ");
        }

        if (!Image.IsAlphaPixelFormat(source.PixelFormat))
        {
            throw new InvalidDataException("PNG 必须包含 Alpha 通道。 ");
        }

        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            // PNG DPI is metadata; atlas coordinates are always physical pixels.
            // DrawImageUnscaled still converts between the source and destination
            // DPI, which enlarged 96-DPI sheets on a 144-DPI desktop.
            graphics.PageUnit = GraphicsUnit.Pixel;
            graphics.PageScale = 1f;
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, source.Width, source.Height),
                new Rectangle(0, 0, source.Width, source.Height),
                GraphicsUnit.Pixel);
        }

        var alpha = new byte[source.Width * source.Height];
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                alpha[(y * source.Width) + x] = source.GetPixel(x, y).A;
            }
        }

        return new DecodedSpriteSheet(bitmap, alpha);
    }

    public byte GetAlpha(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height
        ? Alpha[(y * Width) + x]
        : (byte)0;

    public bool CellHasAlpha(int row, int column, int cellWidth, int cellHeight)
    {
        int startX = column * cellWidth;
        int startY = row * cellHeight;
        for (int y = startY; y < startY + cellHeight; y++)
        {
            int offset = (y * Width) + startX;
            for (int x = 0; x < cellWidth; x++)
            {
                if (Alpha[offset + x] != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void Dispose()
    {
        Bitmap.Dispose();
    }
}
