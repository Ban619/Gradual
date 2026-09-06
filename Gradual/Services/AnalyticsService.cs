using System.Text.Json;

namespace Gradual.Services;

/// <summary>
/// Service for tracking application analytics and user behavior
/// </summary>
public class AnalyticsService
{
    private readonly ConfigurationService _configService;
    private readonly LoggingService _loggingService;
    private readonly string _analyticsFilePath;
    private readonly bool _enabled;
    private AnalyticsSession _currentSession;

    public AnalyticsService(ConfigurationService configService, LoggingService loggingService)
    {
        _configService = configService;
        _loggingService = loggingService;
        _enabled = configService.Settings.Analytics.Enabled;
        
        var dataDir = Path.Combine(
            AppContext.BaseDirectory,
            configService.Settings.ApplicationSettings.DataDirectory);
        Directory.CreateDirectory(dataDir);
        
        _analyticsFilePath = Path.Combine(dataDir, "analytics.json");
        _currentSession = new AnalyticsSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = DateTime.Now
        };

        if (_enabled)
        {
            _loggingService.LogInformation("Analytics tracking initialized. Session ID: {SessionId}", _currentSession.SessionId);
        }
    }

    /// <summary>
    /// Track a user action
    /// </summary>
    public void TrackEvent(string eventName, Dictionary<string, object>? properties = null)
    {
        if (!_enabled || !_configService.Settings.Analytics.TrackUserActions)
            return;

        try
        {
            var eventData = new AnalyticsEvent
            {
                EventId = Guid.NewGuid(),
                SessionId = _currentSession.SessionId,
                EventName = eventName,
                Timestamp = DateTime.Now,
                Properties = properties ?? new Dictionary<string, object>()
            };

            _currentSession.Events.Add(eventData);
            _loggingService.LogDebug("Analytics event tracked: {EventName}", eventName);
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to track analytics event: {EventName}", eventName);
        }
    }

    /// <summary>
    /// Track project creation
    /// </summary>
    public void TrackProjectCreated(string projectName, string status, string priority)
    {
        TrackEvent("ProjectCreated", new Dictionary<string, object>
        {
            { "ProjectName", projectName },
            { "Status", status },
            { "Priority", priority }
        });
    }

    /// <summary>
    /// Track project update
    /// </summary>
    public void TrackProjectUpdated(string projectName, string changeType)
    {
        TrackEvent("ProjectUpdated", new Dictionary<string, object>
        {
            { "ProjectName", projectName },
            { "ChangeType", changeType }
        });
    }

    /// <summary>
    /// Track project deletion
    /// </summary>
    public void TrackProjectDeleted(string projectName)
    {
        TrackEvent("ProjectDeleted", new Dictionary<string, object>
        {
            { "ProjectName", projectName }
        });
    }

    /// <summary>
    /// Track search query
    /// </summary>
    public void TrackSearch(string searchQuery, int resultCount)
    {
        TrackEvent("Search", new Dictionary<string, object>
        {
            { "Query", searchQuery },
            { "ResultCount", resultCount }
        });
    }

    /// <summary>
    /// Track filter usage
    /// </summary>
    public void TrackFilter(string filterType, string filterValue)
    {
        TrackEvent("FilterApplied", new Dictionary<string, object>
        {
            { "FilterType", filterType },
            { "FilterValue", filterValue }
        });
    }

    /// <summary>
    /// Track export operation
    /// </summary>
    public void TrackExport(string exportType, int recordCount)
    {
        TrackEvent("Export", new Dictionary<string, object>
        {
            { "ExportType", exportType },
            { "RecordCount", recordCount }
        });
    }

    /// <summary>
    /// Track performance metric
    /// </summary>
    public void TrackPerformance(string operation, TimeSpan duration)
    {
        if (!_enabled || !_configService.Settings.Analytics.TrackPerformance)
            return;

        TrackEvent("Performance", new Dictionary<string, object>
        {
            { "Operation", operation },
            { "DurationMs", duration.TotalMilliseconds }
        });
    }

    /// <summary>
    /// Get current session statistics
    /// </summary>
    public AnalyticsSessionSummary GetSessionSummary()
    {
        return new AnalyticsSessionSummary
        {
            SessionId = _currentSession.SessionId,
            StartTime = _currentSession.StartTime,
            Duration = DateTime.Now - _currentSession.StartTime,
            TotalEvents = _currentSession.Events.Count,
            EventsByType = _currentSession.Events
                .GroupBy(e => e.EventName)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <summary>
    /// Save analytics data to file
    /// </summary>
    public async Task SaveSessionAsync()
    {
        if (!_enabled)
            return;

        try
        {
            _currentSession.EndTime = DateTime.Now;
            
            List<AnalyticsSession> sessions;
            if (File.Exists(_analyticsFilePath))
            {
                var json = await File.ReadAllTextAsync(_analyticsFilePath);
                sessions = JsonSerializer.Deserialize<List<AnalyticsSession>>(json) ?? new List<AnalyticsSession>();
            }
            else
            {
                sessions = new List<AnalyticsSession>();
            }

            sessions.Add(_currentSession);

            // Keep only last 100 sessions
            if (sessions.Count > 100)
            {
                sessions = sessions.OrderByDescending(s => s.StartTime).Take(100).ToList();
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = JsonSerializer.Serialize(sessions, options);
            await File.WriteAllTextAsync(_analyticsFilePath, updatedJson);

            _loggingService.LogInformation("Analytics session saved. Total events: {EventCount}", _currentSession.Events.Count);
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to save analytics session");
        }
    }
}

public class AnalyticsSession
{
    public Guid SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<AnalyticsEvent> Events { get; set; } = new();
}

public class AnalyticsEvent
{
    public Guid EventId { get; set; }
    public Guid SessionId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class AnalyticsSessionSummary
{
    public Guid SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
}
