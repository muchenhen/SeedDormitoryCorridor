using SeedDormitoryCorridor.App.Logging;
using SeedDormitoryCorridor.App.SingleInstance;
using SeedDormitoryCorridor.Configuration;

namespace SeedDormitoryCorridor.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var paths = new AppPaths();
        paths.EnsureCreated();

        using var logger = new AppLogger(paths.LogsDirectory);
        using var instance = new SingleInstanceCoordinator();
        if (!instance.IsPrimary)
        {
            string command = ParseSecondaryCommand(args);
            _ = SingleInstanceCoordinator.SendAsync(command).GetAwaiter().GetResult();
            return;
        }

        RegisterGlobalExceptions(logger);
        logger.Info("Application starting.");
        logger.LogEnvironment();
        try
        {
            using var context = new PetApplicationContext(paths, logger, instance);
            Application.Run(context);
        }
        catch (Exception exception)
        {
            logger.Error("Fatal startup error.", exception);
            MessageBox.Show($"白荆科技宿舍走廊无法启动：\n{exception.Message}", "启动错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            logger.Info("Application exited.");
        }
    }

    private static string ParseSecondaryCommand(string[] args)
    {
        if (args.Length >= 2 && string.Equals(args[0], "--import", StringComparison.OrdinalIgnoreCase))
        {
            return "import " + Path.GetFullPath(args[1]);
        }

        if (args.Length >= 1 && string.Equals(args[0], "--settings", StringComparison.OrdinalIgnoreCase))
        {
            return "settings";
        }

        if (args.Length >= 1 && (File.Exists(args[0]) || Directory.Exists(args[0])))
        {
            return "import " + Path.GetFullPath(args[0]);
        }

        return "show";
    }

    private static void RegisterGlobalExceptions(AppLogger logger)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) => logger.Error("Unhandled WinForms UI exception.", eventArgs.Exception);
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            logger.Error("Unobserved task exception.", eventArgs.Exception);
            eventArgs.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            logger.Error("Unhandled AppDomain exception.", eventArgs.ExceptionObject as Exception);
    }
}
