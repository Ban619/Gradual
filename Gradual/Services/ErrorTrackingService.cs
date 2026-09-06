using System.Diagnostics;
using System.Text.Json;

namespace Gradual.Services;

/// <summary>
/// Service for tracking and managing application errors
/// </summary>
public class ErrorTrackingService
{
    private readonly ConfigurationService _configService;
    private readonly LoggingService _loggingService;
    private readonly string _errorLogPath;
    private readonly bool _enabled;

    public ErrorTrackingService(ConfigurationService configService, LoggingService loggingService)
    {
        _configService = configService;
        _loggingService = loggingService;
        _enabled = configService.Settings.ErrorTracking.Enabled;
        
        _errorLogPath = Path.Combine(
            configService.GetLogsDirectory(),
            "errors.json");
    }

    /// <summary>
    /// Track an error with full context
    /// </summary>
    public async Task TrackErrorAsync(Exception exception, string context = "", Dictionary<string, object>? additionalData = null)
    {
        if (!_enabled)
            return;

        try
        {
            var errorRecord = new ErrorRecord
            {
                ErrorId = Guid.NewGuid(),
                Timestamp = DateTime.Now,
                ExceptionType = exception.GetType().Name,
                Message = exception.Message,
                StackTrace = exception.StackTrace ?? string.Empty,
                InnerException = exception.InnerException?.Message,
                Context = context,
                AdditionalData = additionalData ?? new Dictionary<string, object>(),
                ApplicationVersion = _configService.Settings.ApplicationSettings.Version
            };

            // Only capture system identity when explicitly opted in (to avoid PII in logs)
            if (_configService.Settings.ErrorTracking.IncludeSystemInfo)
            {
                errorRecord.MachineName = Environment.MachineName;
                errorRecord.UserName = Environment.UserName;
            }

            // Log to file
            if (_configService.Settings.ErrorTracking.LogErrors)
            {
                _loggingService.LogError(exception, "Error in {Context}: {Message}", context, exception.Message);
                await SaveErrorRecordAsync(errorRecord);
            }

            // Email notification (if configured)
            if (_configService.Settings.ErrorTracking.EmailNotifications && 
                !string.IsNullOrWhiteSpace(_configService.Settings.ErrorTracking.NotificationEmail))
            {
                await SendErrorNotificationEmailAsync(errorRecord);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to track error");
        }
    }

    /// <summary>
    /// Track an error with automatic context detection
    /// </summary>
    public async Task TrackErrorAsync(Exception exception)
    {
        var stackTrace = new StackTrace(true);
        var frame = stackTrace.GetFrame(1);
        var method = frame?.GetMethod();
        var context = method != null ? $"{method.DeclaringType?.Name}.{method.Name}" : "Unknown";
        
        await TrackErrorAsync(exception, context);
    }

    /// <summary>
    /// Get error statistics
    /// </summary>
    public async Task<ErrorStatistics> GetErrorStatisticsAsync(DateTime? since = null)
    {
        try
        {
            if (!File.Exists(_errorLogPath))
                return new ErrorStatistics();

            var json = await File.ReadAllTextAsync(_errorLogPath);
            var errors = JsonSerializer.Deserialize<List<ErrorRecord>>(json) ?? new List<ErrorRecord>();

            var sinceDate = since ?? DateTime.Now.AddDays(-7);
            var recentErrors = errors.Where(e => e.Timestamp >= sinceDate).ToList();

            return new ErrorStatistics
            {
                TotalErrors = recentErrors.Count,
                ErrorsByType = recentErrors
                    .GroupBy(e => e.ExceptionType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ErrorsByContext = recentErrors
                    .GroupBy(e => e.Context)
                    .ToDictionary(g => g.Key, g => g.Count()),
                MostRecentError = recentErrors.OrderByDescending(e => e.Timestamp).FirstOrDefault(),
                ErrorsPerDay = recentErrors
                    .GroupBy(e => e.Timestamp.Date)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to get error statistics");
            return new ErrorStatistics();
        }
    }

    /// <summary>
    /// Clear old error records
    /// </summary>
    public async Task CleanupOldErrorsAsync(int daysToKeep = 30)
    {
        try
        {
            if (!File.Exists(_errorLogPath))
                return;

            var json = await File.ReadAllTextAsync(_errorLogPath);
            var errors = JsonSerializer.Deserialize<List<ErrorRecord>>(json) ?? new List<ErrorRecord>();

            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var recentErrors = errors.Where(e => e.Timestamp >= cutoffDate).ToList();

            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = JsonSerializer.Serialize(recentErrors, options);
            await File.WriteAllTextAsync(_errorLogPath, updatedJson);

            _loggingService.LogInformation("Cleaned up old errors. Kept {Count} errors", recentErrors.Count);
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to cleanup old errors");
        }
    }

    private async Task SaveErrorRecordAsync(ErrorRecord errorRecord)
    {
        try
        {
            List<ErrorRecord> errors;
            if (File.Exists(_errorLogPath))
            {
                var json = await File.ReadAllTextAsync(_errorLogPath);
                errors = JsonSerializer.Deserialize<List<ErrorRecord>>(json) ?? new List<ErrorRecord>();
            }
            else
            {
                errors = new List<ErrorRecord>();
            }

            errors.Add(errorRecord);

            // Keep only last 1000 errors
            if (errors.Count > 1000)
            {
                errors = errors.OrderByDescending(e => e.Timestamp).Take(1000).ToList();
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = JsonSerializer.Serialize(errors, options);
            await File.WriteAllTextAsync(_errorLogPath, updatedJson);
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to save error record");
        }
    }

    private async Task SendErrorNotificationEmailAsync(ErrorRecord errorRecord)
    {
        // Placeholder for email notification implementation
        // In production, integrate with SMTP or email service
        await Task.CompletedTask;
        _loggingService.LogInformation("Error notification email would be sent to {Email}", 
            _configService.Settings.ErrorTracking.NotificationEmail);
    }
}

public class ErrorRecord
{
    public Guid ErrorId { get; set; }
    public DateTime Timestamp { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string? InnerException { get; set; }
    public string Context { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ApplicationVersion { get; set; } = string.Empty;
}

public class ErrorStatistics
{
    public int TotalErrors { get; set; }
    public Dictionary<string, int> ErrorsByType { get; set; } = new();
    public Dictionary<string, int> ErrorsByContext { get; set; } = new();
    public ErrorRecord? MostRecentError { get; set; }
    public Dictionary<DateTime, int> ErrorsPerDay { get; set; } = new();
}
