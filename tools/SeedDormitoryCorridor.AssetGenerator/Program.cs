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
    const int atlasWidth = 1536;
    const int atlasHeight = 1872;
    if (source.Width != atlasWidth || source.Height != atlasHeight)
    {
        throw new InvalidDataException(
            $"Sweeper-EX 源图必须已经是 {atlasWidth}×{atlasHeight} 的标准 Atlas，实际为 {source.Width}×{source.Height}。请先按 8×9 网格对齐素材。");
    }

    string outputPath = Path.Combine(outputDirectory, "spritesheet.png");
    if (HasTransparentCanvas(source))
    {
        // Preserve an already-transparent standard Atlas byte for byte. The
        // source intentionally has no physical-DPI chunk, while GDI+'s PNG
        // encoder adds a 96-DPI value that used to change its decoded size.
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, outputPath, overwrite: true);
        }

        // Copy preserves the source timestamp. Touch the generated asset so
        // MSBuild's PreserveNewest rule replaces an older output-directory copy.
        File.SetLastWriteTimeUtc(outputPath, DateTime.UtcNow);
        return;
    }

    using var cleaned = new Bitmap(atlasWidth, atlasHeight, PixelFormat.Format32bppArgb);
    cleaned.SetResolution(source.HorizontalResolution, source.VerticalResolution);
    using (Graphics g = Graphics.FromImage(cleaned))
    {
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.PageUnit = GraphicsUnit.Pixel;
        g.PageScale = 1f;
        g.DrawImage(
            source,
            new Rectangle(0, 0, atlasWidth, atlasHeight),
            new Rectangle(0, 0, atlasWidth, atlasHeight),
            GraphicsUnit.Pixel);
    }

    // Generated previews can contain a checkerboard painted into the image.
    // Only remove neutral pixels connected to the outer border; removing every
    // near-gray pixel also erases the character's white costume and highlights.
    RemoveBorderConnectedBackground(cleaned);

    using var atlas = new Bitmap(atlasWidth, atlasHeight, PixelFormat.Format32bppPArgb);
    atlas.SetResolution(source.HorizontalResolution, source.VerticalResolution);
    using (Graphics g = Graphics.FromImage(atlas))
    {
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceCopy;
        // The source is already a standard Atlas. Do not resample it: even a
        // nominal 1:1 draw can soften edges and shift pixels across cell bounds.
        g.PageUnit = GraphicsUnit.Pixel;
        g.PageScale = 1f;
        g.DrawImage(
            cleaned,
            new Rectangle(0, 0, atlasWidth, atlasHeight),
            new Rectangle(0, 0, atlasWidth, atlasHeight),
            GraphicsUnit.Pixel);
        using var transparent = new SolidBrush(Color.Transparent);
        int[] counts = [6, 8, 8, 4, 5, 8, 6, 6, 6];
        for (int row = 0; row < counts.Length; row++)
        for (int column = counts[row]; column < 8; column++)
            g.FillRectangle(transparent, column * 192, row * 208, 192, 208);
    }
    atlas.Save(outputPath, ImageFormat.Png);
}

static bool HasTransparentCanvas(Bitmap bitmap) =>
    Image.IsAlphaPixelFormat(bitmap.PixelFormat) &&
    bitmap.GetPixel(0, 0).A == 0 &&
    bitmap.GetPixel(bitmap.Width - 1, 0).A == 0 &&
    bitmap.GetPixel(0, bitmap.Height - 1).A == 0 &&
    bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).A == 0;

static void RemoveBorderConnectedBackground(Bitmap bitmap)
{
    int width = bitmap.Width;
    int height = bitmap.Height;
    var visited = new bool[width * height];
    var pending = new Queue<int>();

    void EnqueueIfBackground(int x, int y)
    {
        int index = (y * width) + x;
        if (visited[index])
        {
            return;
        }

        visited[index] = true;
        if (IsBackgroundCandidate(bitmap.GetPixel(x, y)))
        {
            pending.Enqueue(index);
        }
    }

    for (int x = 0; x < width; x++)
    {
        EnqueueIfBackground(x, 0);
        EnqueueIfBackground(x, height - 1);
    }

    for (int y = 1; y < height - 1; y++)
    {
        EnqueueIfBackground(0, y);
        EnqueueIfBackground(width - 1, y);
    }

    while (pending.Count > 0)
    {
        int index = pending.Dequeue();
        int x = index % width;
        int y = index / width;
        bitmap.SetPixel(x, y, Color.Transparent);

        EnqueueNeighbor(x - 1, y);
        EnqueueNeighbor(x + 1, y);
        EnqueueNeighbor(x, y - 1);
        EnqueueNeighbor(x, y + 1);
    }

    void EnqueueNeighbor(int x, int y)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            EnqueueIfBackground(x, y);
        }
    }
}

static bool IsBackgroundCandidate(Color color)
{
    int spread = Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B));
    return color.A == 255 && spread <= 8 && color.R is >= 220 and <= 255 &&
        color.G is >= 220 and <= 255 && color.B is >= 220 and <= 255;
}

internal static class NativeIcon
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint handle);
}
