using SeedDormitoryCorridor.Assets;

namespace SeedDormitoryCorridor.Online.Tests;

public sealed class PetCollectionManagerTests
{
    [Fact]
    public void DeletingCurrentPetSwitchesToDefaultFirst()
    {
        using var directory = new TestDirectory();
        CreateInstalledPet(directory.Pets, "custom");
        PetCollectionManager manager = CreateManager(directory);
        string? switchedTo = null;

        PetDeleteResult result = manager.Delete("custom", "custom", id =>
        {
            switchedTo = id;
            return true;
        });

        Assert.Equal(PetDeleteStatus.Deleted, result.Status);
        Assert.Equal("builtin-seed", switchedTo);
        Assert.False(Directory.Exists(Path.Combine(directory.Pets, "custom")));
    }

    [Fact]
    public void DeletingNonCurrentPetDoesNotSwitch()
    {
        using var directory = new TestDirectory();
        CreateInstalledPet(directory.Pets, "custom");
        PetCollectionManager manager = CreateManager(directory);
        int switches = 0;

        PetDeleteResult result = manager.Delete("custom", "another", _ =>
        {
            switches++;
            return true;
        });

        Assert.Equal(PetDeleteStatus.Deleted, result.Status);
        Assert.Equal(0, switches);
    }

    [Fact]
    public void BuiltInPetIsProtected()
    {
        using var directory = new TestDirectory();
        PetCollectionManager manager = CreateManager(directory);

        PetDeleteResult result = manager.Delete("builtin-seed", "builtin-seed", _ => true);

        Assert.Equal(PetDeleteStatus.BuiltInProtected, result.Status);
    }

    [Fact]
    public void FailedSwitchLeavesCurrentPetInstalled()
    {
        using var directory = new TestDirectory();
        CreateInstalledPet(directory.Pets, "custom");
        PetCollectionManager manager = CreateManager(directory);

        PetDeleteResult result = manager.Delete("custom", "custom", _ => false);

        Assert.Equal(PetDeleteStatus.SwitchFailed, result.Status);
        Assert.True(Directory.Exists(Path.Combine(directory.Pets, "custom")));
    }

    private static PetCollectionManager CreateManager(TestDirectory directory)
    {
        var installer = new PetInstaller(directory.Pets, directory.Staging, new PetPackageLoader());
        return new PetCollectionManager(installer, ["builtin-seed"], "builtin-seed");
    }

    private static void CreateInstalledPet(string petsDirectory, string id)
    {
        string path = Path.Combine(petsDirectory, id);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "pet.json"), "{}");
    }
}
