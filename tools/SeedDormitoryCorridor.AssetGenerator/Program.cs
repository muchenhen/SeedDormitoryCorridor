using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
if (args.Length == 2 && args[0].Equals("--sweeper-ex", StringComparison.OrdinalIgnoreCase))
{
    ProcessSweeperEx(args[1], repositoryRoot);
    return;
}
string outputDirectory = Path.Combine(repositoryRoot, "assets", "builtin-seed");
Directory.CreateDirectory(outputDirectory);

using var atlas = new Bitmap(1536, 1872, PixelFormat.Format32bppPArgb);
using (Graphics graphics = Graphics.FromImage(atlas))
{
    graphics.Clear(Color.Transparent);
    graphics.SmoothingMode = SmoothingMode.AntiAlias;
    int[] counts = [6, 8, 8, 4, 5, 8, 6, 6, 6];
    for (int row = 0; row < counts.Length; row++)
    {
        for (int column = 0; column < counts[row]; column++)
        {
            GraphicsState state = graphics.Save();
            graphics.SetClip(new Rectangle(column * 192, row * 208, 192, 208));
            DrawSeed(graphics, row, column);
            graphics.Restore(state);
        }
    }
}

atlas.Save(Path.Combine(outputDirectory, "spritesheet.png"), ImageFormat.Png);

string iconPath = Path.Combine(repositoryRoot, "assets", "app.ico");
using (var iconBitmap = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
using (Graphics iconGraphics = Graphics.FromImage(iconBitmap))
{
    iconGraphics.Clear(Color.Transparent);
    iconGraphics.ScaleTransform(1.2f, 1.2f);
    DrawSeed(iconGraphics, 0, 0);
    nint iconHandle = iconBitmap.GetHicon();
    try
    {
        using Icon icon = Icon.FromHandle(iconHandle);
        using var stream = new FileStream(iconPath, FileMode.Create, FileAccess.Write, FileShare.None);
        icon.Save(stream);
    }
    finally
    {
        NativeIcon.DestroyIcon(iconHandle);
    }
}

static void DrawSeed(Graphics graphics, int row, int column)
{
    int originX = column * 192;
    int originY = row * 208;
    float bounce = row switch
    {
        4 => -MathF.Sin(column / 4f * MathF.PI) * 42,
        1 or 2 or 7 => (column % 2 == 0 ? -7 : 2),
        _ => (column % 3 == 1 ? -3 : 0),
    };
    float lean = row switch
    {
        1 => 6,
        2 => -6,
        _ => 0,
    };

    graphics.TranslateTransform(originX + 96 + lean, originY + 112 + bounce);
    using var body = new SolidBrush(Color.FromArgb(255, 244, 211, 91));
    using var outline = new Pen(Color.FromArgb(255, 80, 65, 45), 5);
    using var green = new SolidBrush(Color.FromArgb(255, 95, 190, 101));
    using var darkGreen = new Pen(Color.FromArgb(255, 48, 116, 63), 4);
    using var face = new SolidBrush(Color.FromArgb(255, 55, 47, 38));
    using var blush = new SolidBrush(Color.FromArgb(160, 241, 108, 111));

    graphics.FillEllipse(green, -9, -86, 48, 34);
    graphics.DrawEllipse(darkGreen, -9, -86, 48, 34);
    graphics.FillEllipse(green, -38, -82, 43, 30);
    graphics.DrawEllipse(darkGreen, -38, -82, 43, 30);
    graphics.FillEllipse(body, -55, -63, 110, 126);
    graphics.DrawEllipse(outline, -55, -63, 110, 126);

    int blink = row == 0 && column == 2 ? 5 : 12;
    graphics.FillEllipse(face, -27, -20, 10, blink);
    graphics.FillEllipse(face, 17, -20, 10, blink);
    graphics.FillEllipse(blush, -42, 0, 18, 10);
    graphics.FillEllipse(blush, 24, 0, 18, 10);
    graphics.DrawArc(outline, -13, -4, 26, 20, 15, 150);

    using var limb = new Pen(Color.FromArgb(255, 80, 65, 45), 7) { StartCap = LineCap.Round, EndCap = LineCap.Round };
    float arm = row == 3 ? -38 - (column * 7) : 6;
    graphics.DrawLine(limb, -49, 5, -70, row == 3 ? arm : 24);
    graphics.DrawLine(limb, 49, 5, 70, row == 8 ? -18 : 24);
    int stride = row is 1 or 2 or 7 ? (column % 2 == 0 ? 18 : -18) : 0;
    graphics.DrawLine(limb, -24, 57, -34 - stride, 82);
    graphics.DrawLine(limb, 24, 57, 34 + stride, 82);

    if (row == 5)
    {
        using var tear = new SolidBrush(Color.FromArgb(210, 68, 165, 235));
        graphics.FillEllipse(tear, 24, -2, 10, 20);
    }
    else if (row == 6)
    {
        using var bubble = new Pen(Color.FromArgb(180, 100, 170, 230), 4);
        graphics.DrawEllipse(bubble, 53, -71, 18 + column * 2, 18 + column * 2);
    }
    else if (row == 8)
    {
        using var page = new SolidBrush(Color.FromArgb(245, 238, 246, 255));
        graphics.FillRectangle(page, 48, -18, 34, 46);
        graphics.DrawRectangle(outline, 48, -18, 34, 46);
    }

    graphics.ResetTransform();
}

static void ProcessSweeperEx(string sourcePath, string repositoryRoot)
{
    string outputDirectory = Path.Combine(repositoryRoot, "assets", "builtin-sweeper-ex");
    Directory.CreateDirectory(outputDirectory);
    using var source = new Bitmap(sourcePath);
    using var cleaned = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
    using (Graphics g = Graphics.FromImage(cleaned))
    {
        g.Clear(Color.Transparent);
        g.DrawImageUnscaled(source, 0, 0);
    }

    // Generated previews often contain a checkerboard painted into the image.
    // Remove only border-connected near-gray pixels so white costume pixels survive.
    for (int y = 0; y < cleaned.Height; y++)
    for (int x = 0; x < cleaned.Width; x++)
    {
        Color c = cleaned.GetPixel(x, y);
        int spread = Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
        if (c.A == 255 && spread <= 8 && c.R is >= 220 and <= 245 && c.G is >= 220 and <= 245 && c.B is >= 220 and <= 245)
            cleaned.SetPixel(x, y, Color.Transparent);
    }

    using var atlas = new Bitmap(1536, 1872, PixelFormat.Format32bppPArgb);
    using (Graphics g = Graphics.FromImage(atlas))
    {
        g.Clear(Color.Transparent);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(cleaned, new Rectangle(0, 0, atlas.Width, atlas.Height));
        g.CompositingMode = CompositingMode.SourceCopy;
        using var transparent = new SolidBrush(Color.Transparent);
        int[] counts = [6, 8, 8, 4, 5, 8, 6, 6, 6];
        for (int row = 0; row < counts.Length; row++)
        for (int column = counts[row]; column < 8; column++)
            g.FillRectangle(transparent, column * 192, row * 208, 192, 208);
    }
    atlas.Save(Path.Combine(outputDirectory, "spritesheet.png"), ImageFormat.Png);
}

internal static class NativeIcon
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint handle);
}
