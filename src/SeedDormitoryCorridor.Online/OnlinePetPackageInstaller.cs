using System.Security.Cryptography;
using SeedDormitoryCorridor.Assets;

namespace SeedDormitoryCorridor.Online;

public sealed class OnlinePetPackageInstaller
{
    private readonly HttpClient httpClient;
    private readonly string stagingDirectory;
    private readonly PetInstaller installer;
    private readonly PetPackageValidator validator;
    private readonly HashSet<string> protectedPetIds;
    private readonly Version clientVersion;

    public OnlinePetPackageInstaller(
        HttpClient httpClient,
        string stagingDirectory,
        PetInstaller installer,
        PetPackageValidator validator,
        IEnumerable<string>? protectedPetIds = null,
        Version? clientVersion = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.stagingDirectory = Path.GetFullPath(stagingDirectory);
        this.installer = installer ?? throw new ArgumentNullException(nameof(installer));
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        this.protectedPetIds = new HashSet<string>(protectedPetIds ?? [], StringComparer.OrdinalIgnoreCase);
        this.clientVersion = clientVersion ?? new Version(0, 1, 0);
    }

    public async Task<PetInstallResult> InstallAsync(OnlinePetCatalogItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        OnlinePetContract.ValidateItem(item);
        if (!OnlinePetCompatibility.IsCompatible(item, clientVersion))
        {
            throw new OnlinePetLibraryException("install.incompatible", $"宠物 '{item.DisplayName}' 需要客户端 {item.MinimumClientVersion} 或更高版本。");
        }

        if (protectedPetIds.Contains(item.Id))
        {
            throw new OnlinePetLibraryException("install.protected-id", $"在线宠物不能使用内置 id '{item.Id}'。");
        }

        Directory.CreateDirectory(stagingDirectory);
        string transaction = Path.Combine(stagingDirectory, $"online-{Guid.NewGuid():N}");
        Directory.CreateDirectory(transaction);
        string packagePath = Path.Combine(transaction, "package.zip");
        try
        {
            await DownloadPackageAsync(item, packagePath, cancellationToken).ConfigureAwait(false);
            PetPackageValidationReport report = validator.Validate(packagePath);
            if (!report.Valid || report.Package is null)
            {
                throw new OnlinePetLibraryException(
                    "install.package-invalid",
                    "下载的宠物包未通过正式校验。",
                    validationIssues: report.Issues);
            }

            if (!string.Equals(report.Package.Id, item.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new OnlinePetLibraryException(
                    "install.id-mismatch",
                    $"目录 id '{item.Id}' 与宠物包 id '{report.Package.Id}' 不一致。");
            }

            if (report.Package.SpriteVersionNumber != item.SpriteVersionNumber)
            {
                throw new OnlinePetLibraryException(
                    "install.sprite-version-mismatch",
                    $"目录 spriteVersionNumber 与宠物包不一致。");
            }

            PetInstallResult result = installer.Install(packagePath, ExistingPetPolicy.Replace);
            if (!string.Equals(result.PetId, item.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new OnlinePetLibraryException("install.id-mismatch", "安装结果与目录 id 不一致。");
            }

            return result;
        }
        finally
        {
            TryDeleteDirectory(transaction);
        }
    }

    private async Task DownloadPackageAsync(OnlinePetCatalogItem item, string packagePath, CancellationToken cancellationToken)
    {
        var packageUri = new Uri(item.PackageUrl, UriKind.Absolute);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new OnlinePetLibraryException("download.network", exception.Message, exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new OnlinePetLibraryException("download.http", $"下载返回 HTTP {(int)response.StatusCode} ({response.StatusCode})。");
            }

            OnlinePetContract.EnsureHttps(response.RequestMessage?.RequestUri ?? packageUri, "download.redirect");
            if (response.Content.Headers.ContentLength is long contentLength && contentLength != item.PackageSize)
            {
                throw new OnlinePetLibraryException("download.size", "下载响应大小与目录声明不一致。");
            }

            try
            {
                await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var output = new FileStream(
                    packagePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[64 * 1024];
                long total = 0;
                while (true)
                {
                    int read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    total += read;
                    if (total > item.PackageSize || total > OnlinePetContract.MaximumPackageBytes)
                    {
                        throw new OnlinePetLibraryException("download.size", "下载内容超过目录声明大小。");
                    }

                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                if (total != item.PackageSize)
                {
                    throw new OnlinePetLibraryException("download.size", "下载内容大小与目录声明不一致。");
                }

                byte[] actualHash = hash.GetHashAndReset();
                byte[] expectedHash = Convert.FromHexString(item.Sha256);
                if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
                {
                    throw new OnlinePetLibraryException("download.sha256", "下载内容的 SHA-256 与目录声明不一致。");
                }
            }
            catch (OnlinePetLibraryException)
            {
                throw;
            }
            catch (IOException exception)
            {
                throw new OnlinePetLibraryException("download.interrupted", exception.Message, exception);
            }
            catch (HttpRequestException exception)
            {
                throw new OnlinePetLibraryException("download.network", exception.Message, exception);
            }
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
            // Best-effort cleanup; online transactions are never reused.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup; online transactions are never reused.
        }
    }
}
