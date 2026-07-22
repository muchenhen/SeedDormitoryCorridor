using System.Security.Cryptography;
using SeedDormitoryCorridor.Assets;
using SeedDormitoryCorridor.Online;

namespace SeedDormitoryCorridor.Online.Tests;

public sealed class OnlinePetPackageInstallerTests
{
    [Fact]
    public async Task RejectsSha256MismatchAndCleansStaging()
    {
        byte[] package = TestSupport.CreateValidPackage();
        using var directory = new TestDirectory();
        OnlinePetCatalogItem item = TestSupport.CreateCatalogItem(package, sha256: new string('0', 64));
        using HttpClient client = TestSupport.CreateHttpClient(_ => TestSupport.BinaryResponse(package));
        OnlinePetPackageInstaller service = CreateService(client, directory);

        OnlinePetLibraryException exception = await Assert.ThrowsAsync<OnlinePetLibraryException>(() => service.InstallAsync(item));

        Assert.Equal("download.sha256", exception.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Staging));
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Pets));
    }

    [Fact]
    public async Task ReportsInterruptedDownloadAndCleansStaging()
    {
        byte[] package = TestSupport.CreateValidPackage();
        using var directory = new TestDirectory();
        OnlinePetCatalogItem item = TestSupport.CreateCatalogItem(package);
        using HttpClient client = TestSupport.CreateHttpClient(_ =>
            TestSupport.StreamResponse(new InterruptingStream(package, package.Length / 2), package.Length));
        OnlinePetPackageInstaller service = CreateService(client, directory);

        OnlinePetLibraryException exception = await Assert.ThrowsAsync<OnlinePetLibraryException>(() => service.InstallAsync(item));

        Assert.Equal("download.interrupted", exception.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Staging));
    }

    [Fact]
    public async Task RejectsPackageThatFailsSharedValidator()
    {
        byte[] package = "not a zip"u8.ToArray();
        using var directory = new TestDirectory();
        OnlinePetCatalogItem item = TestSupport.CreateCatalogItem(package);
        using HttpClient client = TestSupport.CreateHttpClient(_ => TestSupport.BinaryResponse(package));
        OnlinePetPackageInstaller service = CreateService(client, directory);

        OnlinePetLibraryException exception = await Assert.ThrowsAsync<OnlinePetLibraryException>(() => service.InstallAsync(item));

        Assert.Equal("install.package-invalid", exception.Code);
        Assert.NotEmpty(exception.ValidationIssues);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Staging));
    }

    [Fact]
    public async Task ReinstallAtomicallyReplacesExistingPet()
    {
        byte[] package = TestSupport.CreateValidPackage();
        using var directory = new TestDirectory();
        OnlinePetCatalogItem item = TestSupport.CreateCatalogItem(package);
        using HttpClient client = TestSupport.CreateHttpClient(_ => TestSupport.BinaryResponse(package));
        OnlinePetPackageInstaller service = CreateService(client, directory);

        PetInstallResult first = await service.InstallAsync(item);
        PetInstallResult second = await service.InstallAsync(item);

        Assert.Equal(item.Id, first.PetId);
        Assert.Equal(first.InstallPath, second.InstallPath);
        Assert.True(File.Exists(Path.Combine(second.InstallPath, "pet.json")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Staging));
    }

    [Fact]
    public async Task RejectsIncompatiblePetBeforeDownload()
    {
        byte[] package = TestSupport.CreateValidPackage();
        using var directory = new TestDirectory();
        OnlinePetCatalogItem original = TestSupport.CreateCatalogItem(package);
        OnlinePetCatalogItem item = TestSupport.CopyCatalogItem(original, minimumClientVersion: "9.0.0");
        int requests = 0;
        using HttpClient client = TestSupport.CreateHttpClient(_ =>
        {
            requests++;
            return TestSupport.BinaryResponse(package);
        });
        OnlinePetPackageInstaller service = CreateService(client, directory);

        OnlinePetLibraryException exception = await Assert.ThrowsAsync<OnlinePetLibraryException>(() => service.InstallAsync(item));

        Assert.Equal("install.incompatible", exception.Code);
        Assert.Equal(0, requests);
    }

    private static OnlinePetPackageInstaller CreateService(HttpClient client, TestDirectory directory)
    {
        var loader = new PetPackageLoader();
        var installer = new PetInstaller(directory.Pets, directory.Staging, loader);
        var validator = new PetPackageValidator(directory.Staging, loader);
        return new OnlinePetPackageInstaller(client, directory.Staging, installer, validator, clientVersion: new Version(0, 1, 0));
    }
}

internal sealed class InterruptingStream(byte[] bytes, int interruptAfter) : Stream
{
    private int position;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => bytes.Length;
    public override long Position { get => position; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (position >= interruptAfter)
        {
            throw new IOException("Simulated connection interruption.");
        }

        int available = Math.Min(count, Math.Min(interruptAfter - position, bytes.Length - position));
        Array.Copy(bytes, position, buffer, offset, available);
        position += available;
        return available;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (position >= interruptAfter)
        {
            return ValueTask.FromException<int>(new IOException("Simulated connection interruption."));
        }

        int available = Math.Min(buffer.Length, Math.Min(interruptAfter - position, bytes.Length - position));
        bytes.AsMemory(position, available).CopyTo(buffer);
        position += available;
        return ValueTask.FromResult(available);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
