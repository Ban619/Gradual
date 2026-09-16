using Serilog;
using Serilog.Events;

namespace Gradual.Services;

/// <summary>
/// Centralized logging service using Serilog
/// </summary>
public class LoggingService
{
    private readonly ILogger _logger;

    /// <summary>Normal constructor — sets up Serilog with file + console sinks.</summary>
    public LoggingService(ConfigurationService configService)
    {
        var logSettings = configService.Settings.Logging;
        var logPath = Path.Combine(
            configService.GetLogsDirectory(),
            "gradual-.log");

        var logConfig = new LoggerConfiguration()
            .MinimumLevel.Is(ParseLogLevel(logSettings.MinimumLevel))
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Gradual")
            .Enrich.WithProperty("Version", configService.Settings.ApplicationSettings.Version);

        if (logSettings.EnableConsole)
        {
            logConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        if (logSettings.EnableFile)
        {
            logConfig.WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: logSettings.RetainedFileCountLimit,
                fileSizeLimitBytes: logSettings.FileSizeLimitBytes,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        _logger = logConfig.CreateLogger();
        Log.Logger = _logger;
    }

    /// <summary>
    /// Private constructor used by <see cref="CreateNoop"/> — discards all log calls.
    /// This is the zero-cost placeholder used while the real logger initialises in the background.
    /// </summary>
    private LoggingService(ILogger noopLogger)
    {
        _logger = noopLogger;
    }

    /// <summary>
    /// Returns a LoggingService that silently discards all messages.
    /// Used as a placeholder during background logger initialisation.
    /// </summary>
    public static LoggingService CreateNoop()
        => new(Serilog.Core.Logger.None);

    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "verbose"     => LogEventLevel.Verbose,
            "debug"       => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning"     => LogEventLevel.Warning,
            "error"       => LogEventLevel.Error,
            "fatal"       => LogEventLevel.Fatal,
            _             => LogEventLevel.Information
        };
    }

    public void LogInformation(string message, params object[] args) => _logger.Information(message, args);
    public void LogWarning    (string message, params object[] args) => _logger.Warning    (message, args);
    public void LogError      (string message, params object[] args) => _logger.Error      (message, args);
    public void LogError(Exception exception, string message, params object[] args) => _logger.Error(exception, message, args);
    public void LogDebug      (string message, params object[] args) => _logger.Debug      (message, args);
    public void LogFatal(Exception exception, string message, params object[] args) => _logger.Fatal(exception, message, args);

    public void Dispose() => Log.CloseAndFlush();
}
