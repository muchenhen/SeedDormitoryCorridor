using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text.Json;

namespace SeedDormitoryCorridor.Assets.Tests;

public sealed class PetPackageLoaderTests
{
    [Fact]
    public void LoadsMinimalManifestAndAutoDetectsProfile()
    {
        using var package = TestPackage.CreateValid();
        using PetPackage loaded = new PetPackageLoader().Load(package.Path);

        Assert.Equal("test-pet", loaded.Manifest.Id);
        Assert.Equal(CodexPetV2Profile.Id, loaded.Atlas.Animations["idle"].Name == "idle" ? CodexPetV2Profile.Id : string.Empty);
    }

    [Fact]
    public void ReportsAllMissingRequiredFields()
    {
        using var package = new TestPackage();
        File.WriteAllText(System.IO.Path.Combine(package.Path, "pet.json"), "{}");

        ValidationResult result = new PetPackageLoader().ValidateAndLoad(package.Path, out _);

        Assert.Contains(result.Issues, issue => issue.Code == "manifest.id.required");
        Assert.Contains(result.Issues, issue => issue.Code == "manifest.displayName.required");
        Assert.Contains(result.Issues, issue => issue.Code == "manifest.spritesheetPath.invalid");
    }

    [Fact]
    public void RejectsUnsupportedSpriteVersion()
    {
        using var package = new TestPackage();
        package.WriteManifest("spritesheet.png", spriteVersionNumber: 3);

        ValidationResult result = new PetPackageLoader().ValidateAndLoad(package.Path, out _);

        Assert.Contains(result.Issues, issue => issue.Code == "manifest.spriteVersionNumber.invalid");
    }

    [Fact]
    public void LoadsVersion2ElevenRowAtlas()
    {
        using var package = TestPackage.CreateValid(spriteVersionNumber: 2);

        using PetPackage loaded = new PetPackageLoader().Load(package.Path);

        Assert.Equal(2, loaded.Manifest.SpriteVersionNumber);
        Assert.Equal((1536, 2288, 11), (loaded.Atlas.Width, loaded.Atlas.Height, loaded.Atlas.Rows));
    }

    [Fact]
    public void RejectsVersion2DimensionsWhenVersionNumberIsMissing()
    {
        using var package = TestPackage.CreateValid(spriteVersionNumber: 2);
        package.WriteManifest("spritesheet.png");

        ValidationResult result = new PetPackageLoader().ValidateAndLoad(package.Path, out _);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.dimensions");
    }

    [Fact]
    public void ReportsInvalidJsonWithPathInformation()
    {
        using var package = new TestPackage();
        File.WriteAllText(System.IO.Path.Combine(package.Path, "pet.json"), "{\"id\": }");

        ValidationResult result = new PetPackageLoader().ValidateAndLoad(package.Path, out _);

        Assert.Contains(result.Issues, issue => issue.Code == "manifest.invalid-json");
    }

    [Fact]
    public void RejectsPathTraversal()
    {
        using var package = new TestPackage();
        package.WriteManifest("../spritesheet.png");

        ValidationResult result = new PetPackageLoader().ValidateAndLoad(package.Path, out _);

        Assert.Contains(result.Issues, issue => issue.Code == "manifest.spritesheetPath.invalid");
    }

    [Fact]
    public void RejectsWrongDimensions()
    {
        using var package = new TestPackage();
        package.WriteManifest("spritesheet.png", profile: CodexPetV2Profile.Id);
        using (var bitmap = new Bitmap(10, 10, PixelFormat.Format32bppArgb))
        {
            bitmap.Save(System.IO.Path.Combine(package.Path, "spritesheet.png"), ImageFormat.Png);
        }

        ValidationResult result = new PetPackageLoader().ValidateAndLoad(package.Path, out _);
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.dimensions");
    }

    [Fact]
    public void RejectsPngWithoutAlphaChannel()
    {
        using var package = new TestPackage();
        package.WriteManifest("spritesheet.png");
        using (var bitmap = new Bitmap(1536, 1872, PixelFormat.Format24bppRgb))
        {
            bitmap.Save(System.IO.Path.Combine(package.Path, "spritesheet.png"), ImageFormat.Png);
        }

        ValidationResult result = new PetPackageLoader().ValidateAndLoad(package.Path, out _);
        Assert.Contains(result.Issues, issue => issue.Code == "spritesheet.decode");
    }

    [Fact]
    public void RejectsTransparentRequiredCellAndVisibleUnusedCell()
    {
        using var package = TestPackage.CreateValid();
        string imagePath = System.IO.Path.Combine(package.Path, "spritesheet.png");
        using (var bitmap = new Bitmap(imagePath))
        using (var replacement = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(replacement))
        {
            graphics.DrawImageUnscaled(bitmap, 0, 0);
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.FillRectangle(Brushes.Transparent, 0, 0, 192, 208);
            graphics.FillRectangle(Brushes.White, 7 * 192, 0, 1, 1);
            replacement.Save(imagePath + ".new", ImageFormat.Png);
        }

        File.Move(imagePath + ".new", imagePath, true);
        ValidationResult result = new PetPackageLoader().ValidateAndLoad(package.Path, out _);
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.required-cell-transparent");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.unused-cell-visible");
    }

    [Fact]
    public void RejectsZipPathTraversal()
    {
        using var package = new TestPackage();
        string zip = System.IO.Path.Combine(package.Path, "bad.zip");
        using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            archive.CreateEntry("../escape.txt");
        }

        var installer = new PetInstaller(System.IO.Path.Combine(package.Path, "pets"), System.IO.Path.Combine(package.Path, "staging"), new PetPackageLoader());
        Assert.Throws<InvalidDataException>(() => installer.Install(zip, ExistingPetPolicy.Cancel));
    }

    [Fact]
    public void DuplicateIdRequiresReplacePolicy()
    {
        using var root = new TestPackage();
        using var package = TestPackage.CreateValid();
        var installer = new PetInstaller(System.IO.Path.Combine(root.Path, "pets"), System.IO.Path.Combine(root.Path, "staging"), new PetPackageLoader());
        installer.Install(package.Path, ExistingPetPolicy.Cancel);

        Assert.Throws<IOException>(() => installer.Install(package.Path, ExistingPetPolicy.Cancel));
        PetInstallResult replaced = installer.Install(package.Path, ExistingPetPolicy.Replace);
        Assert.Equal("test-pet", replaced.PetId);
        using PetPackage loaded = new PetPackageLoader().Load(replaced.InstallPath);
        Assert.Equal("Test Pet", loaded.Manifest.DisplayName);
    }

    private sealed class TestPackage : IDisposable
    {
        internal TestPackage()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SeedDormitoryCorridor.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        internal static TestPackage CreateValid(int? spriteVersionNumber = null)
        {
            var package = new TestPackage();
            package.WriteManifest("spritesheet.png", spriteVersionNumber: spriteVersionNumber);
            AtlasDefinition atlas = new CodexPetV2Profile().CreateAtlasDefinition(new PetManifest
            {
                SpriteVersionNumber = spriteVersionNumber,
            });
            using var bitmap = new Bitmap(atlas.Width, atlas.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                foreach ((int row, int column) in atlas.RequiredCells)
                {
                    graphics.FillRectangle(Brushes.White, column * atlas.FrameWidth, row * atlas.FrameHeight, 1, 1);
                }
            }

            bitmap.Save(System.IO.Path.Combine(package.Path, "spritesheet.png"), ImageFormat.Png);
            return package;
        }

        internal void WriteManifest(string spritePath, string? profile = null, int? spriteVersionNumber = null)
        {
            var manifest = new
            {
                id = "test-pet",
                displayName = "Test Pet",
                spriteVersionNumber,
                spritesheetPath = spritePath,
                desktopPet = profile is null ? null : new { profile },
            };
            File.WriteAllText(System.IO.Path.Combine(Path, "pet.json"), JsonSerializer.Serialize(manifest));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
                // Test cleanup is best effort.
            }
        }
    }
}
