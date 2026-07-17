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
}
