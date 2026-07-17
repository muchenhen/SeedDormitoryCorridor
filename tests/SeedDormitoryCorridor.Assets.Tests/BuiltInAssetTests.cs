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
}
