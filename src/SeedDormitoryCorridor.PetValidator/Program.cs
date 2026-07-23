using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using SeedDormitoryCorridor.Assets;

namespace SeedDormitoryCorridor.PetValidator;

internal static class Program
{
    private static int Main(string[] args) => PetValidatorApplication.Run(args, Console.Out, Console.Error);
}

public static class PetValidatorApplication
{
    public const int ValidExitCode = 0;
    public const int InvalidPackageExitCode = 1;
    public const int UsageOrRuntimeErrorExitCode = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The CLI contract requires unexpected failures to return exit code 2.")]
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParseArguments(args, out ValidatorArguments? parsed, out string? argumentError))
        {
            OutputFormat requestedFormat = DetectRequestedFormat(args);
            WriteFailure(requestedFormat, output, error, "cli.arguments", argumentError!, null, includeUsage: true);
            return UsageOrRuntimeErrorExitCode;
        }

        ValidatorArguments arguments = parsed!;
        string sourcePath;
        try
        {
            sourcePath = Path.GetFullPath(arguments.SourcePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            WriteFailure(arguments.Format, output, error, "cli.path.invalid", exception.Message, arguments.SourcePath);
            return UsageOrRuntimeErrorExitCode;
        }

        if (!Directory.Exists(sourcePath) && !File.Exists(sourcePath))
        {
            WriteFailure(arguments.Format, output, error, "cli.source.not-found", "找不到指定的宠物包路径。", sourcePath);
            return UsageOrRuntimeErrorExitCode;
        }

        try
        {
            var validator = new PetPackageValidator();
            PetPackageValidationReport report = validator.Validate(sourcePath);
            WriteReport(arguments.Format, output, report);
            return report.Valid ? ValidExitCode : InvalidPackageExitCode;
        }
        catch (Exception exception)
        {
            WriteFailure(arguments.Format, output, error, "cli.runtime", exception.Message, sourcePath);
            return UsageOrRuntimeErrorExitCode;
        }
    }

    private static bool TryParseArguments(string[] args, out ValidatorArguments? parsed, out string? error)
    {
        parsed = null;
        error = null;
        if (args.Length < 2 || !string.Equals(args[0], "validate", StringComparison.OrdinalIgnoreCase))
        {
            error = "用法：sdc-pet-validator validate <pet.zip|directory> [--format json|text]";
            return false;
        }

        OutputFormat format = OutputFormat.Text;
        bool formatSpecified = false;
        for (int index = 2; index < args.Length; index++)
        {
            if (!string.Equals(args[index], "--format", StringComparison.OrdinalIgnoreCase) ||
                formatSpecified || index + 1 >= args.Length)
            {
                error = $"无法识别参数 '{args[index]}'。";
                return false;
            }

            string value = args[++index];
            if (string.Equals(value, "json", StringComparison.OrdinalIgnoreCase))
            {
                format = OutputFormat.Json;
            }
            else if (string.Equals(value, "text", StringComparison.OrdinalIgnoreCase))
            {
                format = OutputFormat.Text;
            }
            else
            {
                error = "--format 只能是 json 或 text。";
                return false;
            }

            formatSpecified = true;
        }

        parsed = new ValidatorArguments(args[1], format);
        return true;
    }

    private static OutputFormat DetectRequestedFormat(string[] args)
    {
        for (int index = 0; index + 1 < args.Length; index++)
        {
            if (string.Equals(args[index], "--format", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(args[index + 1], "json", StringComparison.OrdinalIgnoreCase))
            {
                return OutputFormat.Json;
            }
        }

        return OutputFormat.Text;
    }

    private static void WriteFailure(
        OutputFormat format,
        TextWriter output,
        TextWriter error,
        string code,
        string message,
        string? filePath,
        bool includeUsage = false)
    {
        var issue = new ValidationIssue(ValidationSeverity.Error, code, message, FilePath: filePath);
        var report = new PetPackageValidationReport(false, null, [issue]);
        if (format == OutputFormat.Json)
        {
            WriteReport(format, output, report);
            return;
        }

        error.WriteLine($"[{code}] {message}");
        if (includeUsage)
        {
            error.WriteLine("用法：sdc-pet-validator validate <pet.zip|directory> [--format json|text]");
        }
    }

    private static void WriteReport(OutputFormat format, TextWriter output, PetPackageValidationReport report)
    {
        if (format == OutputFormat.Json)
        {
            var document = new JsonReport(
                report.Valid,
                report.Package,
                report.Issues.Select(issue => new JsonIssue(
                    issue.Severity.ToString().ToLowerInvariant(),
                    issue.Code,
                    issue.Message,
                    issue.JsonPath,
                    issue.FilePath)).ToArray());
            output.WriteLine(JsonSerializer.Serialize(document, JsonOptions));
            return;
        }

        output.WriteLine(report.Valid ? "VALID" : "INVALID");
        if (report.Package is not null)
        {
            output.WriteLine($"{report.Package.Id} ({report.Package.DisplayName})");
            output.WriteLine($"{report.Package.Profile} {report.Package.Width}x{report.Package.Height}");
        }

        foreach (ValidationIssue issue in report.Issues)
        {
            output.WriteLine($"[{issue.Code}] {issue.Message}");
        }
    }

    private enum OutputFormat
    {
        Text,
        Json,
    }

    private sealed record ValidatorArguments(string SourcePath, OutputFormat Format);

    private sealed record JsonReport(bool Valid, PetPackageInformation? Package, IReadOnlyList<JsonIssue> Issues);

    private sealed record JsonIssue(string Severity, string Code, string Message, string? JsonPath, string? FilePath);
}
