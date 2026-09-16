namespace Gradual.Configuration;

/// <summary>
/// Root configuration settings for the application
/// </summary>
public class AppSettings
{
    public ApplicationSettings ApplicationSettings { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public AnalyticsSettings Analytics { get; set; } = new();
    public WebhooksSettings Webhooks { get; set; } = new();
    public ErrorTrackingSettings ErrorTracking { get; set; } = new();
    public ValidationSettings Validation { get; set; } = new();
    public UISettings UI { get; set; } = new();
    public GitHubSettings GitHub { get; set; } = new();
}

public class ApplicationSettings
{
    public string ApplicationName { get; set; } = "Gradual";
    public string Version { get; set; } = "2.0.0";
    public string DataDirectory { get; set; } = "data";
    public string ProjectsFileName { get; set; } = "projects.json";
    public string BackupDirectory { get; set; } = "backups";
    public bool EnableAutoBackup { get; set; } = true;
    public int BackupIntervalMinutes { get; set; } = 30;
    public int MaxBackupFiles { get; set; } = 10;
}

public class LoggingSettings
{
    public string LogDirectory { get; set; } = "logs";
    public string MinimumLevel { get; set; } = "Information";
    public bool EnableConsole { get; set; } = true;
    public bool EnableFile { get; set; } = true;
    public int RetainedFileCountLimit { get; set; } = 30;
    public long FileSizeLimitBytes { get; set; } = 10485760;
}

public class AnalyticsSettings
{
    public bool Enabled { get; set; } = true;
    public bool TrackUserActions { get; set; } = true;
    public bool TrackPerformance { get; set; } = true;
    public int SessionTimeout { get; set; } = 30;
}

public class WebhooksSettings
{
    public bool Enabled { get; set; } = false;
    public List<WebhookEndpoint> Endpoints { get; set; } = new();
}

public class WebhookEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
}

public class ErrorTrackingSettings
{
    public bool Enabled { get; set; } = true;
    public bool LogErrors { get; set; } = true;
    public bool ShowDetailedErrors { get; set; } = false;
    public bool EmailNotifications { get; set; } = false;
    public string NotificationEmail { get; set; } = string.Empty;
    /// <summary>
    /// When false (default), MachineName and UserName are omitted from error
    /// records to avoid persisting PII. Set to true only in trusted dev environments.
    /// </summary>
    public bool IncludeSystemInfo { get; set; } = false;
}

public class ValidationSettings
{
    public int MaxProjectNameLength { get; set; } = 200;
    public int MaxClientNameLength { get; set; } = 200;
    public int MaxNotesLength { get; set; } = 5000;
    public int MaxFolderPathLength { get; set; } = 500;
    public int MaxPhases { get; set; } = 50;
    public int MaxPhaseLength { get; set; } = 100;
    public int MaxAttachments { get; set; } = 100;
}

public class UISettings
{
    public string DefaultTheme { get; set; } = "Dark";
    public bool EnableAnimations { get; set; } = true;
    public int AutoRefreshInterval { get; set; } = 0;
    public int DefaultPageSize { get; set; } = 100;
}

/// <summary>
/// GitHub integration settings.
/// IMPORTANT: Do NOT place a real PAT in appsettings.json when committing to source control.
/// Use the GRADUAL_GITHUB_PAT environment variable instead.
/// The PersonalAccessToken field here is a fallback for local development only.
/// </summary>
public class GitHubSettings
{
    /// <summary>Fallback PAT — prefer the GRADUAL_GITHUB_PAT environment variable.</summary>
    public string PersonalAccessToken { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = "Gradual";
    public bool EnableSync { get; set; } = false;
    public int SyncIntervalMinutes { get; set; } = 30;
    public int MaxCommitsPerSync { get; set; } = 100;
    public int MaxIssuesPerSync { get; set; } = 50;
    public int CacheExpirationMinutes { get; set; } = 15;
    public int RateLimitWarningThreshold { get; set; } = 100;
    public string DefaultBranch { get; set; } = "main";
}
