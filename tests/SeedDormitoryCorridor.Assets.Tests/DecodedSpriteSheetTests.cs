using System.Drawing;
using System.Drawing.Imaging;

namespace SeedDormitoryCorridor.Assets.Tests;

public sealed class DecodedSpriteSheetTests
{
    [Fact]
    public void DecodePngUsesPixelCoordinatesRegardlessOfPngDpi()
    {
        string path = Path.Combine(Path.GetTempPath(), $"seed-dormitory-corridor-{Guid.NewGuid():N}.png");
        try
        {
            using (var source = new Bitmap(2, 2, PixelFormat.Format32bppArgb))
            {
                source.SetResolution(72, 72);
                source.SetPixel(0, 0, Color.Red);
                source.SetPixel(1, 0, Color.Green);
                source.SetPixel(0, 1, Color.Blue);
                source.SetPixel(1, 1, Color.White);
                source.Save(path, ImageFormat.Png);
            }

            using DecodedSpriteSheet decoded = DecodedSpriteSheet.DecodePng(path);

            Assert.Equal(Color.Red.ToArgb(), decoded.Bitmap.GetPixel(0, 0).ToArgb());
            Assert.Equal(Color.Green.ToArgb(), decoded.Bitmap.GetPixel(1, 0).ToArgb());
            Assert.Equal(Color.Blue.ToArgb(), decoded.Bitmap.GetPixel(0, 1).ToArgb());
            Assert.Equal(Color.White.ToArgb(), decoded.Bitmap.GetPixel(1, 1).ToArgb());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
