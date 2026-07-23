namespace SeedDormitoryCorridor.Assets;

public sealed record PetPackageInformation(
    string Id,
    string DisplayName,
    string? Description,
    int SpriteVersionNumber,
    string Profile,
    int Width,
    int Height);

public sealed record PetPackageValidationReport(
    bool Valid,
    PetPackageInformation? Package,
    IReadOnlyList<ValidationIssue> Issues);

/// <summary>Validates an untrusted directory or ZIP without writing to the installed-pets directory.</summary>
public sealed class PetPackageValidator
{
    private readonly PetPackageLoader loader;
    private readonly string stagingDirectory;

    public PetPackageValidator(string? stagingDirectory = null, PetPackageLoader? loader = null)
    {
        this.stagingDirectory = Path.GetFullPath(stagingDirectory ??
            Path.Combine(Path.GetTempPath(), "SeedDormitoryCorridor", "pet-validator"));
        this.loader = loader ?? new PetPackageLoader();
    }

    public PetPackageValidationReport Validate(string sourcePath)
    {
        try
        {
            using PetPackageStagingSession staged = PetPackageStagingSession.Create(sourcePath, stagingDirectory);
            ValidationResult validation = loader.ValidateAndLoad(staged.PackageRoot, out PetPackage? package);
            using (package)
            {
                ValidationIssue[] issues = validation.Issues
                    .Select(issue => NormalizeIssuePath(issue, staged.PackageRoot))
                    .ToArray();
                if (!validation.IsValid || package is null)
                {
                    return new PetPackageValidationReport(false, null, issues);
                }

                var information = new PetPackageInformation(
                    package.Manifest.Id!,
                    package.Manifest.DisplayName!,
                    package.Manifest.Description,
                    package.Manifest.SpriteVersionNumber ?? 1,
                    package.ProfileId,
                    package.Atlas.Width,
                    package.Atlas.Height);
                return new PetPackageValidationReport(true, information, issues);
            }
        }
        catch (PetPackageStagingException exception)
        {
            var issue = new ValidationIssue(
                ValidationSeverity.Error,
                exception.Code,
                exception.Message,
                FilePath: exception.FilePath);
            return new PetPackageValidationReport(false, null, [issue]);
        }
    }

    private static ValidationIssue NormalizeIssuePath(ValidationIssue issue, string packageRoot)
    {
        if (string.IsNullOrEmpty(issue.FilePath) || !Path.IsPathFullyQualified(issue.FilePath))
        {
            return issue;
        }

        string relative = Path.GetRelativePath(packageRoot, issue.FilePath);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return issue;
        }

        return issue with { FilePath = relative.Replace('\\', '/') };
    }
}
