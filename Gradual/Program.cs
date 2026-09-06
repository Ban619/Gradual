using Gradual.Infrastructure;

namespace Gradual;

static class Program
{
    [STAThread]
    static void Main()
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");

        // ── Global unhandled exception handlers ────────────────────────────────
        // Catches exceptions that escape async void event handlers (e.g. ThreadException)
        // and ensures they are logged rather than silently crashing the application.
        Application.ThreadException += (sender, args) =>
        {
            LogUnhandledException(args.Exception, logPath, "ThreadException");
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nSee startup-error.log for details.",
                "Unhandled Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogUnhandledException(ex, logPath, "UnhandledException");
            }
        };

        // Ensure ThreadException handler fires before the default dialog
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        try
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ApplicationConfiguration.Initialize();

            // Configure dependency injection (configuration loaded from appsettings.json)
            ServiceContainer.ConfigureServices();

            using var form = new GradForm();
            Application.Run(form);

            // Clean up DI container
            ServiceContainer.Dispose();
        }
        catch (Exception ex)
        {
            LogUnhandledException(ex, logPath, "Startup");
            MessageBox.Show(
                $"Gradual could not start.\n\n{ex.Message}\n\nSee startup-error.log for details.",
                "Startup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void LogUnhandledException(Exception ex, string logPath, string context)
    {
        try
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:u}] [{context}] {ex}\n");
        }
        catch
        {
            // If we can't write to the log file, there's nothing more we can do.
        }
    }
}