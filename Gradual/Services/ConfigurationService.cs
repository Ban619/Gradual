using Gradual.Configuration;
using Microsoft.Extensions.Configuration;

namespace Gradual.Services;

/// <summary>
/// Service for managing application configuration
/// </summary>
public class ConfigurationService
{
    private readonly IConfiguration _configuration;
    private AppSettings _settings;

    public ConfigurationService(string configFilePath = "appsettings.json")
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(configFilePath, optional: false, reloadOnChange: true)
            .Build();

        _settings = new AppSettings();
        _configuration.Bind(_settings);
    }

    /// <summary>
    /// Live-bound settings — re-reads from the (auto-reloaded) IConfiguration on every access
    /// so that changes written to appsettings.json are visible without a restart.
    /// </summary>
    public AppSettings Settings
    {
        get
        {
            // Re-bind every time so post-save writes are picked up immediately.
            _configuration.Bind(_settings);
            return _settings;
        }
    }

    /// <summary>Force-reload settings from disk (useful after an explicit file write).</summary>
    public void Reload()
    {
        if (_configuration is IConfigurationRoot root)
            root.Reload();
        _settings = new AppSettings();
        _configuration.Bind(_settings);
    }

    public T GetSection<T>(string sectionName) where T : new()
    {
        var section = new T();
        _configuration.GetSection(sectionName).Bind(section);
        return section;
    }

    public string GetConnectionString(string name)
    {
        return _configuration.GetConnectionString(name) ?? string.Empty;
    }

    public string GetValue(string key, string defaultValue = "")
    {
        return _configuration[key] ?? defaultValue;
    }

    /// <summary>
    /// Gets the full path to the projects data file
    /// </summary>
    public string GetProjectsDataPath()
    {
        var dataDir = Path.Combine(
            AppContext.BaseDirectory,
            _settings.ApplicationSettings.DataDirectory);
        
        Directory.CreateDirectory(dataDir);
        
        return Path.Combine(dataDir, _settings.ApplicationSettings.ProjectsFileName);
    }

    /// <summary>
    /// Gets the full path to the logs directory
    /// </summary>
    public string GetLogsDirectory()
    {
        var logsDir = Path.Combine(
            AppContext.BaseDirectory,
            _settings.Logging.LogDirectory);
        
        Directory.CreateDirectory(logsDir);
        return logsDir;
    }

    /// <summary>
    /// Gets the full path to the backups directory
    /// </summary>
    public string GetBackupsDirectory()
    {
        var backupsDir = Path.Combine(
            AppContext.BaseDirectory,
            _settings.ApplicationSettings.BackupDirectory);
        
        Directory.CreateDirectory(backupsDir);
        return backupsDir;
    }

    /// <summary>
    /// Returns the GitHub Personal Access Token.
    /// Priority: GRADUAL_GITHUB_PAT environment variable → appsettings.json fallback.
    /// Never hard-code a real PAT in appsettings.json when committing to source control.
    /// </summary>
    public string GetGitHubPat()
    {
        var envPat = Environment.GetEnvironmentVariable("GRADUAL_GITHUB_PAT");
        if (!string.IsNullOrWhiteSpace(envPat))
            return envPat;

        return _settings.GitHub.PersonalAccessToken;
    }
}
