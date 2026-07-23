namespace SeedDormitoryCorridor.Assets;

public enum ExistingPetPolicy
{
    Cancel,
    Replace,
}

public sealed record PetInstallResult(string PetId, string InstallPath, IReadOnlyList<ValidationIssue> Issues);

/// <summary>Stages, validates and transactionally installs untrusted directory or ZIP packages.</summary>
public sealed class PetInstaller
{
    private readonly string petsDirectory;
    private readonly string stagingDirectory;
    private readonly PetPackageLoader loader;

    public PetInstaller(string petsDirectory, string stagingDirectory, PetPackageLoader loader)
    {
        this.petsDirectory = Path.GetFullPath(petsDirectory);
        this.stagingDirectory = Path.GetFullPath(stagingDirectory);
        this.loader = loader;
    }

    public PetInstallResult Install(string sourcePath, ExistingPetPolicy policy)
    {
        Directory.CreateDirectory(petsDirectory);
        using PetPackageStagingSession staged = PetPackageStagingSession.Create(sourcePath, stagingDirectory);
        using PetPackage package = loader.Load(staged.PackageRoot);
        string id = package.Manifest.Id!;
        EnsureSafePetId(id);
        string destination = Path.Combine(petsDirectory, id);
        if (Directory.Exists(destination) && policy == ExistingPetPolicy.Cancel)
        {
            throw new IOException($"宠物 '{id}' 已存在，导入已取消。");
        }

        string ready = Path.Combine(staged.TransactionRoot, "ready");
        staged.MovePackageRootTo(ready);
        Commit(ready, destination, policy);
        return new PetInstallResult(id, destination, []);
    }

    public IReadOnlyList<(string Id, string Path)> ListInstalled()
    {
        if (!Directory.Exists(petsDirectory))
        {
            return [];
        }

        var pets = new List<(string Id, string Path)>();
        foreach (string directory in Directory.EnumerateDirectories(petsDirectory))
        {
            if (File.Exists(Path.Combine(directory, "pet.json")))
            {
                pets.Add((Path.GetFileName(directory), directory));
            }
        }

        return pets;
    }

    public void Delete(string petId)
    {
        EnsureSafePetId(petId);

        string target = PathSecurity.ResolveWithinRoot(petsDirectory, petId);
        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }
    }

    private static void EnsureSafePetId(string petId)
    {
        if (!PathSecurity.IsSafeRelativePath(petId) ||
            petId.Contains(Path.DirectorySeparatorChar) ||
            petId.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Invalid pet id.", nameof(petId));
        }
    }

    private static void Commit(string ready, string destination, ExistingPetPolicy policy)
    {
        if (!Directory.Exists(destination))
        {
            Directory.Move(ready, destination);
            return;
        }

        if (policy != ExistingPetPolicy.Replace)
        {
            throw new IOException("目标宠物已存在。");
        }

        string backup = destination + $".backup-{Guid.NewGuid():N}";
        Directory.Move(destination, backup);
        try
        {
            Directory.Move(ready, destination);
            TryDeleteDirectory(backup);
        }
        catch
        {
            if (!Directory.Exists(destination) && Directory.Exists(backup))
            {
                Directory.Move(backup, destination);
            }

            throw;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; the next startup can remove stale staging folders.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup; validation never uses leftover folders.
        }
    }
}
