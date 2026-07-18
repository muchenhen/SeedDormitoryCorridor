namespace SeedDormitoryCorridor.Assets.Tests;

public sealed class BuiltInAssetTests
{
    [Fact]
    public void ShippedBuiltInPetPassesTheSameValidationPipeline()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string assetRoot = Path.Combine(repositoryRoot, "assets", "builtin-seed");

        using PetPackage package = new PetPackageLoader().Load(assetRoot);

        Assert.Equal("builtin-seed", package.Manifest.Id);
        Assert.Equal(9, package.Atlas.Animations.All.Count);
    }

    [Fact]
    public void ShippedSuXiaoDefaultPetPassesTheSameValidationPipeline()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string assetRoot = Path.Combine(repositoryRoot, "assets", "builtin-su-xiao");

        using PetPackage package = new PetPackageLoader().Load(assetRoot);

        Assert.Equal("builtin-su-xiao", package.Manifest.Id);
        Assert.Equal("苏筱", package.Manifest.DisplayName);
        Assert.Equal("spritesheet-chat-output.png", package.Manifest.SpritesheetPath);
        Assert.Equal((1536, 1872), (package.Atlas.Width, package.Atlas.Height));
    }

    [Fact]
    public void ShippedTianRuoPetPassesTheSameValidationPipeline()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string assetRoot = Path.Combine(repositoryRoot, "assets", "builtin-tian-ruo");

        using PetPackage package = new PetPackageLoader().Load(assetRoot);

        Assert.Equal("tian-ruo", package.Manifest.Id);
        Assert.Equal("田偌", package.Manifest.DisplayName);
        Assert.Equal(2, package.Manifest.SpriteVersionNumber);
        Assert.Equal((1536, 2288, 11), (package.Atlas.Width, package.Atlas.Height, package.Atlas.Rows));
    }

    [Fact]
    public void ShippedSweeperExPetPassesTheSameValidationPipeline()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string assetRoot = Path.Combine(repositoryRoot, "assets", "builtin-sweeper-ex");

        using PetPackage package = new PetPackageLoader().Load(assetRoot);

        Assert.Equal("builtin-sweeper-ex", package.Manifest.Id);
        Assert.Equal("Sweeper-EX", package.Manifest.DisplayName);
        Assert.Equal((1536, 1872, 9), (package.Atlas.Width, package.Atlas.Height, package.Atlas.Rows));
    }
}
