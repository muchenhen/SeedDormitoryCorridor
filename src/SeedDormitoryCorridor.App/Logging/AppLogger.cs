using System.Globalization;
using System.Reflection;
using System.Text;

namespace SeedDormitoryCorridor.App.Logging;

public sealed class AppLogger : IDisposable
{
    private readonly object gate = new();
    private readonly StreamWriter writer;

    public AppLogger(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);
        string path = Path.Combine(logsDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
        writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    public void LogEnvironment()
    {
        string version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        Info($"Version={version}; OS={Environment.OSVersion.VersionString}; Runtime={Environment.Version}; x64={Environment.Is64BitProcess}");
    }

    public void Dispose()
    {
        lock (gate)
        {
            writer.Dispose();
        }
    }

    private void Write(string level, string message, Exception? exception)
    {
        lock (gate)
        {
            writer.Write(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
            writer.Write(' ');
            writer.Write(level);
            writer.Write(' ');
            writer.WriteLine(message.Replace(Environment.NewLine, " ", StringComparison.Ordinal));
            if (exception is not null)
            {
                writer.WriteLine(exception);
            }
        }
    }
}
