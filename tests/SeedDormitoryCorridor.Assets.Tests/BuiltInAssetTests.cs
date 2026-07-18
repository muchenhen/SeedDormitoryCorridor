using SeedDormitoryCorridor.Runtime;

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

        // This light costume pixel used to be removed by the Sweeper-EX
        // generator's global near-gray cleanup, leaving visible holes in frames.
        Assert.True(package.SpriteSheet.GetAlpha(519, 37) >= 16);

        foreach (AnimationDefinition animation in package.Atlas.Animations.All)
        {
            for (int column = 0; column < animation.FrameCount; column++)
            {
                int opaquePixels = CountOpaquePixels(package, animation.Row, column);
                Assert.True(opaquePixels > 100,
                    $"Sweeper-EX animation '{animation.Name}' frame {column} is blank or nearly blank.");
            }
        }

        var player = new AnimationPlayer(package.Atlas.Animations, "idle");
        long timestamp = 0;
        foreach (AnimationDefinition animation in package.Atlas.Animations.All.OrderByDescending(item => item.Priority))
        {
            Assert.True(player.Play(animation.Name, timestamp++, animation.Priority, force: true, restart: true));
            Assert.Equal(animation.Name, player.State.AnimationName);
            Assert.Equal(0, player.State.Column);
        }
    }

    private static int CountOpaquePixels(PetPackage package, int row, int column)
    {
        int count = 0;
        int startX = column * package.Atlas.FrameWidth;
        int startY = row * package.Atlas.FrameHeight;
        for (int y = startY; y < startY + package.Atlas.FrameHeight; y++)
        {
            for (int x = startX; x < startX + package.Atlas.FrameWidth; x++)
            {
                if (package.SpriteSheet.GetAlpha(x, y) >= 16)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
