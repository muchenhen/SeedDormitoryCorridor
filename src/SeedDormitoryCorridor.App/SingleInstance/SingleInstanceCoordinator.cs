using System.IO.Pipes;
using System.Text;

namespace SeedDormitoryCorridor.App.SingleInstance;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = "Local\\SeedDormitoryCorridor-51D63DD5-5E96-4A60-AC56-BA303DDAB7E1";
    private const string PipeName = "SeedDormitoryCorridor-51D63DD5-5E96-4A60-AC56-BA303DDAB7E1";
    private readonly Mutex mutex;
    private readonly bool ownsMutex;
    private CancellationTokenSource? cancellation;
    private Task? serverTask;

    public SingleInstanceCoordinator()
    {
        mutex = new Mutex(true, MutexName, out ownsMutex);
    }

    public bool IsPrimary => ownsMutex;

    public static async Task<bool> SendAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!IsValidCommand(command))
        {
            return false;
        }

        try
        {
            await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1500, cancellationToken).ConfigureAwait(false);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true);
            await writer.WriteLineAsync(command.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public void StartServer(Action<string> commandReceived)
    {
        if (!IsPrimary || serverTask is not null)
        {
            throw new InvalidOperationException("Only the primary instance can start one IPC server.");
        }

        cancellation = new CancellationTokenSource();
        serverTask = RunServerAsync(commandReceived, cancellation.Token);
    }

    public void Dispose()
    {
        cancellation?.Cancel();
        try
        {
            serverTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Cancellation during shutdown is expected.
        }

        cancellation?.Dispose();
        if (ownsMutex)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
    }

    public static bool IsValidCommand(string command) => command is "show" or "hide" or "settings" ||
        command.StartsWith("import ", StringComparison.Ordinal) && command.Length > 7;

    private static async Task RunServerAsync(Action<string> commandReceived, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(pipe, Encoding.UTF8, true, 1024, leaveOpen: true);
                string? command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (command is not null && IsValidCommand(command))
                {
                    commandReceived(command);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
