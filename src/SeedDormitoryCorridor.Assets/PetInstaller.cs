using System.IO.Compression;

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
    private const int MaximumEntries = 256;
    private const long MaximumEntryBytes = 64L * 1024 * 1024;
    private const long MaximumExpandedBytes = 128L * 1024 * 1024;
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
        Directory.CreateDirectory(stagingDirectory);
        string transaction = Path.Combine(stagingDirectory, Guid.NewGuid().ToString("N"));
        string payload = Path.Combine(transaction, "payload");
        Directory.CreateDirectory(payload);
        try
        {
            if (Directory.Exists(sourcePath))
            {
                CopyDirectorySecure(sourcePath, payload);
            }
            else if (File.Exists(sourcePath) && string.Equals(Path.GetExtension(sourcePath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                ExtractZipSecure(sourcePath, payload);
            }
            else
            {
                throw new FileNotFoundException("请选择宠物目录或 ZIP 文件。", sourcePath);
            }

            string packageRoot = LocatePackageRoot(payload);
            using PetPackage package = loader.Load(packageRoot);
            string id = package.Manifest.Id!;
            string destination = Path.Combine(petsDirectory, id);
            if (Directory.Exists(destination) && policy == ExistingPetPolicy.Cancel)
            {
                throw new IOException($"宠物 '{id}' 已存在，导入已取消。");
            }

            string ready = Path.Combine(transaction, "ready");
            Directory.Move(packageRoot, ready);
            Commit(ready, destination, policy);
            return new PetInstallResult(id, destination, []);
        }
        finally
        {
            TryDeleteDirectory(transaction);
        }
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
        if (!PathSecurity.IsSafeRelativePath(petId) || petId.Contains(Path.DirectorySeparatorChar))
        {
            throw new ArgumentException("Invalid pet id.", nameof(petId));
        }

        string target = PathSecurity.ResolveWithinRoot(petsDirectory, petId);
        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }
    }

    private static string LocatePackageRoot(string payload)
    {
        string direct = Path.Combine(payload, "pet.json");
        if (File.Exists(direct))
        {
            return payload;
        }

        string[] manifests = Directory.EnumerateFiles(payload, "pet.json", SearchOption.AllDirectories).Take(2).ToArray();
        return manifests.Length switch
        {
            0 => throw new InvalidDataException("导入包中找不到 pet.json。"),
            1 => Path.GetDirectoryName(manifests[0])!,
            _ => throw new InvalidDataException("导入包只能包含一个 pet.json。"),
        };
    }

    private static void CopyDirectorySecure(string source, string destination)
    {
        string sourceRoot = Path.GetFullPath(source);
        var rootInfo = new DirectoryInfo(sourceRoot);
        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("不能导入重解析点目录。");
        }

        long total = 0;
        int count = 0;
        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("宠物目录不能包含重解析点。");
            }

            if (++count > MaximumEntries || info.Length > MaximumEntryBytes || (total += info.Length) > MaximumExpandedBytes)
            {
                throw new InvalidDataException("宠物包超过文件数量或大小限制。");
            }

            string relative = Path.GetRelativePath(sourceRoot, file);
            string target = PathSecurity.ResolveWithinRoot(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, false);
        }
    }

    private static void ExtractZipSecure(string zipPath, string destination)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > MaximumEntries)
        {
            throw new InvalidDataException("ZIP 条目数量超过限制。");
        }

        long total = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            if (entry.Length > MaximumEntryBytes || (total += entry.Length) > MaximumExpandedBytes)
            {
                throw new InvalidDataException("ZIP 解压后大小超过限制。");
            }

            string target = PathSecurity.ResolveWithinRoot(destination, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using Stream input = entry.Open();
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
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
