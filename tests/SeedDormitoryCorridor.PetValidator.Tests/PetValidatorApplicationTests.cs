using System.Text.Json;

namespace SeedDormitoryCorridor.PetValidator.Tests;

public sealed class PetValidatorApplicationTests
{
    [Fact]
    public void ValidDirectoryReturnsJsonAndExitCodeZero()
    {
        string source = GetBuiltInPetPath();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = PetValidatorApplication.Run(["validate", source, "--format", "json"], output, error);

        Assert.Equal(PetValidatorApplication.ValidExitCode, exitCode);
        Assert.Empty(error.ToString());
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.True(document.RootElement.GetProperty("valid").GetBoolean());
        Assert.Equal("builtin-seed", document.RootElement.GetProperty("package").GetProperty("id").GetString());
        Assert.Equal("codex-pet-v2", document.RootElement.GetProperty("package").GetProperty("profile").GetString());
        Assert.Empty(document.RootElement.GetProperty("issues").EnumerateArray());
    }

    [Fact]
    public void InvalidDirectoryReturnsStructuredIssueAndExitCodeOne()
    {
        using var directory = new TemporaryDirectory();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = PetValidatorApplication.Run(["validate", directory.Path, "--format", "json"], output, error);

        Assert.Equal(PetValidatorApplication.InvalidPackageExitCode, exitCode);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.False(document.RootElement.GetProperty("valid").GetBoolean());
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("issues").EnumerateArray());
        Assert.Equal("package.manifest.missing", issue.GetProperty("code").GetString());
        Assert.Equal("pet.json", issue.GetProperty("filePath").GetString());
    }

    [Fact]
    public void MissingSourceReturnsJsonAndExitCodeTwo()
    {
        using var directory = new TemporaryDirectory();
        string missing = Path.Combine(directory.Path, "missing.zip");
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = PetValidatorApplication.Run(["validate", missing, "--format", "json"], output, error);

        Assert.Equal(PetValidatorApplication.UsageOrRuntimeErrorExitCode, exitCode);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("issues").EnumerateArray());
        Assert.Equal("cli.source.not-found", issue.GetProperty("code").GetString());
    }

    [Fact]
    public void UnknownArgumentReturnsTextUsageAndExitCodeTwo()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = PetValidatorApplication.Run(["unknown"], output, error);

        Assert.Equal(PetValidatorApplication.UsageOrRuntimeErrorExitCode, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains("用法", error.ToString(), StringComparison.Ordinal);
    }

    private static string GetBuiltInPetPath()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(repositoryRoot, "assets", "builtin-seed");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SeedDormitoryCorridor.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
                // Test cleanup is best effort.
            }
        }
    }
}
