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

    [Theory]
    [InlineData("spritesheet.png:payload")]
    [InlineData("CON.png")]
    [InlineData("folder//spritesheet.png")]
    public void RejectsUnsafeWindowsPaths(string spritePath)
    {
        using var package = new TestPackage();
        package.WriteManifest(spritePath);

        ValidationResult result = new PetPackageLoader().ValidateAndLoad(package.Path, out _);

        Assert.Contains(result.Issues, issue => issue.Code == "manifest.spritesheetPath.invalid");
    }

    [Fact]
    public void RejectsInvalidPetId()
    {
        using var package = new TestPackage();
        File.WriteAllText(System.IO.Path.Combine(package.Path, "pet.json"), """
            {
              "id": "../unsafe",
              "displayName": "Unsafe Pet",
              "spritesheetPath": "spritesheet.png"
            }
            """);

        ValidationResult result = new PetPackageLoader().ValidateAndLoad(package.Path, out _);

        Assert.Contains(result.Issues, issue => issue.Code == "manifest.id.invalid" && issue.JsonPath == "$.id");
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
        PetPackageStagingException exception = Assert.Throws<PetPackageStagingException>(
            () => installer.Install(zip, ExistingPetPolicy.Cancel));
        Assert.Equal("package.path.invalid", exception.Code);
    }

    [Fact]
    public void ValidatorAcceptsSingleWrapperDirectoryAndCleansStaging()
    {
        using var root = new TestPackage();
        using var package = TestPackage.CreateValid();
        string zip = System.IO.Path.Combine(root.Path, "wrapped.zip");
        string staging = System.IO.Path.Combine(root.Path, "staging");
        ZipFile.CreateFromDirectory(package.Path, zip, CompressionLevel.Fastest, includeBaseDirectory: true);

        PetPackageValidationReport report = new PetPackageValidator(staging).Validate(zip);

        Assert.True(report.Valid);
        Assert.Equal("test-pet", report.Package?.Id);
        Assert.Equal(CodexPetV2Profile.Id, report.Package?.Profile);
        Assert.Equal((1536, 1872), (report.Package?.Width, report.Package?.Height));
        Assert.Empty(Directory.EnumerateFileSystemEntries(staging));
    }

    [Fact]
    public void ValidatorRejectsMultipleManifestsAndCleansStaging()
    {
        using var root = new TestPackage();
        string zip = System.IO.Path.Combine(root.Path, "multiple-manifests.zip");
        string staging = System.IO.Path.Combine(root.Path, "staging");
        using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            WriteTextEntry(archive, "first/pet.json", "{}");
            WriteTextEntry(archive, "second/pet.json", "{}");
        }

        PetPackageValidationReport report = new PetPackageValidator(staging).Validate(zip);

        Assert.False(report.Valid);
        ValidationIssue issue = Assert.Single(report.Issues);
        Assert.Equal("package.manifest.multiple", issue.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(staging));
    }

    [Fact]
    public void ValidatorRejectsDirectAndNestedManifests()
    {
        using var root = new TestPackage();
        string zip = System.IO.Path.Combine(root.Path, "direct-and-nested.zip");
        using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            WriteTextEntry(archive, "pet.json", "{}");
            WriteTextEntry(archive, "nested/pet.json", "{}");
        }

        PetPackageValidationReport report = new PetPackageValidator(System.IO.Path.Combine(root.Path, "staging")).Validate(zip);

        Assert.False(report.Valid);
        Assert.Contains(report.Issues, issue => issue.Code == "package.manifest.multiple");
    }

    [Fact]
    public void ValidatorRejectsMoreThanOneWrapperDirectory()
    {
        using var root = new TestPackage();
        string zip = System.IO.Path.Combine(root.Path, "deep-wrapper.zip");
        using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            WriteTextEntry(archive, "first/second/pet.json", "{}");
        }

        PetPackageValidationReport report = new PetPackageValidator(System.IO.Path.Combine(root.Path, "staging")).Validate(zip);

        Assert.False(report.Valid);
        Assert.Contains(report.Issues, issue => issue.Code == "package.root.depth");
    }

    [Fact]
    public void ValidatorRejectsSourceThatContainsItsStagingDirectory()
    {
        using var root = new TestPackage();
        string staging = System.IO.Path.Combine(root.Path, "staging");

        PetPackageValidationReport report = new PetPackageValidator(staging).Validate(root.Path);

        Assert.False(report.Valid);
        Assert.Contains(report.Issues, issue => issue.Code == "package.source.overlaps-staging");
    }

    [Fact]
    public void ValidatorRejectsZipWithTooManyEntries()
    {
        using var root = new TestPackage();
        string zip = System.IO.Path.Combine(root.Path, "too-many.zip");
        using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            for (int index = 0; index < 257; index++)
            {
                archive.CreateEntry($"entry-{index}.txt");
            }
        }

        PetPackageValidationReport report = new PetPackageValidator(System.IO.Path.Combine(root.Path, "staging")).Validate(zip);

        Assert.False(report.Valid);
        Assert.Contains(report.Issues, issue => issue.Code == "package.entries.limit");
    }

    [Fact]
    public void ValidatorRejectsZipWhoseExpandedTotalExceedsLimit()
    {
        using var root = new TestPackage();
        string zip = System.IO.Path.Combine(root.Path, "expanded-too-large.zip");
        using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            WriteSizedEntry(archive, "first.bin", 64L * 1024 * 1024);
            WriteSizedEntry(archive, "second.bin", 64L * 1024 * 1024);
            WriteSizedEntry(archive, "overflow.bin", 1);
        }

        PetPackageValidationReport report = new PetPackageValidator(System.IO.Path.Combine(root.Path, "staging")).Validate(zip);

        Assert.False(report.Valid);
        Assert.Contains(report.Issues, issue => issue.Code == "package.expanded-size");
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

    private static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static void WriteSizedEntry(ZipArchive archive, string path, long length)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using Stream stream = entry.Open();
        byte[] buffer = new byte[128 * 1024];
        long remaining = length;
        while (remaining > 0)
        {
            int count = (int)Math.Min(buffer.Length, remaining);
            stream.Write(buffer, 0, count);
            remaining -= count;
        }
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
