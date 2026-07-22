using System.IO.Compression;

namespace SeedDormitoryCorridor.Assets;

public sealed class PetPackageStagingException : IOException
{
    public PetPackageStagingException(string code, string message, string? filePath = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        FilePath = filePath;
    }

    public string Code { get; }

    public string? FilePath { get; }
}

internal sealed class PetPackageStagingSession : IDisposable
{
    internal const int MaximumEntries = 256;
    internal const long MaximumEntryBytes = 64L * 1024 * 1024;
    internal const long MaximumExpandedBytes = 128L * 1024 * 1024;

    private bool packageRootMoved;

    private PetPackageStagingSession(string transactionRoot, string packageRoot)
    {
        TransactionRoot = transactionRoot;
        PackageRoot = packageRoot;
    }

    internal string TransactionRoot { get; }

    internal string PackageRoot { get; }

    internal static PetPackageStagingSession Create(string sourcePath, string stagingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);

        string stagingRoot = Path.GetFullPath(stagingDirectory);
        if (Directory.Exists(sourcePath))
        {
            string sourceRoot = Path.GetFullPath(sourcePath);
            if (IsWithinRoot(sourceRoot, stagingRoot))
            {
                throw new PetPackageStagingException(
                    "package.source.overlaps-staging",
                    "宠物包源目录不能包含校验 staging 目录。",
                    sourceRoot);
            }
        }

        Directory.CreateDirectory(stagingRoot);
        string transaction = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
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
            else if (File.Exists(sourcePath))
            {
                throw new PetPackageStagingException(
                    "package.source.unsupported",
                    "宠物包必须是包含 pet.json 的目录或 ZIP 文件。",
                    sourcePath);
            }
            else
            {
                throw new PetPackageStagingException(
                    "package.source.not-found",
                    "找不到宠物包目录或 ZIP 文件。",
                    sourcePath);
            }

            string packageRoot = LocatePackageRoot(payload);
            return new PetPackageStagingSession(transaction, packageRoot);
        }
        catch
        {
            TryDeleteDirectory(transaction);
            throw;
        }
    }

    internal void MovePackageRootTo(string destination)
    {
        if (packageRootMoved)
        {
            throw new InvalidOperationException("The staged package root has already been moved.");
        }

        Directory.Move(PackageRoot, destination);
        packageRootMoved = true;
    }

    public void Dispose() => TryDeleteDirectory(TransactionRoot);

    private static string LocatePackageRoot(string payload)
    {
        string[] manifests = Directory.EnumerateFiles(payload, "pet.json", SearchOption.AllDirectories).Take(2).ToArray();
        if (manifests.Length != 1)
        {
            throw manifests.Length == 0
                ? new PetPackageStagingException("package.manifest.missing", "导入包中找不到 pet.json。", "pet.json")
                : new PetPackageStagingException("package.manifest.multiple", "导入包只能包含一个 pet.json。", "pet.json");
        }

        string packageRoot = Path.GetDirectoryName(manifests[0])!;
        string relativeRoot = Path.GetRelativePath(payload, packageRoot);
        if (relativeRoot != "." && relativeRoot.Contains(Path.DirectorySeparatorChar))
        {
            throw new PetPackageStagingException(
                "package.root.depth",
                "pet.json 只能位于 ZIP 根目录或一层包装目录中。",
                NormalizeRelativePath(Path.GetRelativePath(payload, manifests[0])));
        }

        return packageRoot;
    }

    private static void CopyDirectorySecure(string source, string destination)
    {
        string sourceRoot = Path.GetFullPath(source);
        var rootInfo = new DirectoryInfo(sourceRoot);
        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new PetPackageStagingException(
                "package.directory.reparse-point",
                "不能导入重解析点目录。",
                sourceRoot);
        }

        long total = 0;
        int count = 0;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(rootInfo);
        while (pending.Count > 0)
        {
            DirectoryInfo directory = pending.Pop();
            foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
            {
                string relative = Path.GetRelativePath(sourceRoot, entry.FullName);
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new PetPackageStagingException(
                        "package.entry.reparse-point",
                        "宠物目录不能包含重解析点。",
                        NormalizeRelativePath(relative));
                }

                if (entry is DirectoryInfo childDirectory)
                {
                    pending.Push(childDirectory);
                    continue;
                }

                var file = (FileInfo)entry;
                count++;
                if (count > MaximumEntries)
                {
                    throw new PetPackageStagingException(
                        "package.entries.limit",
                        $"宠物包文件数量不能超过 {MaximumEntries}。",
                        NormalizeRelativePath(relative));
                }

                if (file.Length > MaximumEntryBytes)
                {
                    throw new PetPackageStagingException(
                        "package.entry.size",
                        $"宠物包单个文件不能超过 {MaximumEntryBytes} 字节。",
                        NormalizeRelativePath(relative));
                }

                total += file.Length;
                if (total > MaximumExpandedBytes)
                {
                    throw new PetPackageStagingException(
                        "package.expanded-size",
                        $"宠物包总大小不能超过 {MaximumExpandedBytes} 字节。",
                        NormalizeRelativePath(relative));
                }

                string target = ResolveStagingTarget(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target))
                {
                    throw new PetPackageStagingException(
                        "package.entry.duplicate",
                        "宠物目录包含重复的目标路径。",
                        NormalizeRelativePath(relative));
                }

                File.Copy(file.FullName, target, false);
            }
        }
    }

    private static void ExtractZipSecure(string zipPath, string destination)
    {
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count > MaximumEntries)
            {
                throw new PetPackageStagingException(
                    "package.entries.limit",
                    $"ZIP 条目数量不能超过 {MaximumEntries}。",
                    Path.GetFileName(zipPath));
            }

            long total = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                if (entry.Length > MaximumEntryBytes)
                {
                    throw new PetPackageStagingException(
                        "package.entry.size",
                        $"ZIP 单个条目解压后不能超过 {MaximumEntryBytes} 字节。",
                        entry.FullName);
                }

                total += entry.Length;
                if (total > MaximumExpandedBytes)
                {
                    throw new PetPackageStagingException(
                        "package.expanded-size",
                        $"ZIP 解压后总大小不能超过 {MaximumExpandedBytes} 字节。",
                        entry.FullName);
                }

                string target = ResolveStagingTarget(destination, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target))
                {
                    throw new PetPackageStagingException(
                        "package.entry.duplicate",
                        "ZIP 中包含重复的目标路径。",
                        entry.FullName);
                }

                using Stream input = entry.Open();
                using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
            }
        }
        catch (PetPackageStagingException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            throw new PetPackageStagingException(
                "package.zip.invalid",
                $"ZIP 文件无效：{exception.Message}",
                Path.GetFileName(zipPath),
                exception);
        }
    }

    private static string ResolveStagingTarget(string destination, string relativePath)
    {
        try
        {
            return PathSecurity.ResolveWithinRoot(destination, relativePath);
        }
        catch (InvalidDataException exception)
        {
            throw new PetPackageStagingException(
                "package.path.invalid",
                exception.Message,
                NormalizeRelativePath(relativePath),
                exception);
        }
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

    private static bool IsWithinRoot(string root, string candidate)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
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
            // Best-effort cleanup; validation never reuses stale staging folders.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup; validation never reuses stale staging folders.
        }
    }
}
