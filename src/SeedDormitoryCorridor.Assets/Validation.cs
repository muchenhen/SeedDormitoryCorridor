namespace SeedDormitoryCorridor.Assets;

public enum ValidationSeverity
{
    Warning,
    Error,
}

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? JsonPath = null,
    string? FilePath = null);

public sealed class ValidationResult
{
    private readonly List<ValidationIssue> issues = [];

    public IReadOnlyList<ValidationIssue> Issues => issues;

    public bool IsValid => issues.All(issue => issue.Severity != ValidationSeverity.Error);

    public void Add(ValidationIssue issue) => issues.Add(issue);

    public void AddError(string code, string message, string? jsonPath = null, string? filePath = null) =>
        issues.Add(new ValidationIssue(ValidationSeverity.Error, code, message, jsonPath, filePath));

    public void AddWarning(string code, string message, string? jsonPath = null, string? filePath = null) =>
        issues.Add(new ValidationIssue(ValidationSeverity.Warning, code, message, jsonPath, filePath));
}

public sealed class PetValidationException(ValidationResult validation) : Exception(
    string.Join(Environment.NewLine, validation.Issues.Select(issue => $"[{issue.Code}] {issue.Message}")))
{
    public ValidationResult Validation { get; } = validation;
}
