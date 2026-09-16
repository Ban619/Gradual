using Gradual.Interfaces;
using Gradual.Models.DTOs;
using Gradual.Services;
using Gradual.Services.GitHub;
using Gradual.Repositories.GitHub;
using Gradual.Validation;
using Gradual.BusinessLogic.Rules;
using Gradual.BusinessLogic.Workflows;
using Gradual.BusinessLogic.Financial;
using Gradual.BusinessLogic.Resources;
using Gradual.BusinessLogic.Milestones;
using Gradual.BusinessLogic.Approvals;
using Gradual.BusinessLogic.Dependencies;
using Gradual.BusinessLogic.Intelligence;
using Gradual.BusinessLogic.Notifications;
using Gradual.BusinessLogic.Audit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Gradual.Infrastructure;

/// <summary>
/// Simple dependency injection container for the application
/// Registers and resolves services
/// </summary>
public static class ServiceContainer
{
    private static IServiceProvider? _serviceProvider;
    private static System.Threading.Timer? _backupTimer;
    private static Services.GitHub.GitHubSyncWorkflow? _syncWorkflow;
    // Cached IConfiguration so ConfigurationBuilder is only run once.
    private static IConfiguration? _builtConfig;

    /// <summary>
    /// Configures and builds the service container
    /// </summary>
    public static void ConfigureServices()
    {
        var services = new ServiceCollection();

        // ── Tier 1: MUST be synchronous (needed before BuildServiceProvider) ──────

        // ConfigurationService reads appsettings.json — unavoidable, but fast (~5ms)
        var configService = new ConfigurationService();
        services.AddSingleton(configService);

        // Reuse the IConfiguration already built inside ConfigurationService
        // so ConfigurationBuilder is never called a second time.
        var configRoot = configService.GetType()
            .GetField("_configuration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(configService) as IConfiguration;
        _builtConfig = configRoot ?? new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        services.AddSingleton<IConfiguration>(_ => _builtConfig!);

        // LoggingService sets up the Serilog file sink — deferred to background.
        // A no-op logger is used until the real one is ready (< 200ms on SSD).
        var loggingServiceHolder = new LoggingServiceHolder();
        services.AddSingleton<LoggingService>(sp => loggingServiceHolder.Value);

        // ILogger (Serilog global) — also deferred, always current after background init
        services.AddSingleton<ILogger>(_ => Log.Logger);

        // ── Tier 2: lazy factories — constructed only when first accessed ──────────

        // AnalyticsService — only needed after the window is shown
        services.AddSingleton<AnalyticsService>(sp =>
            new AnalyticsService(
                sp.GetRequiredService<ConfigurationService>(),
                loggingServiceHolder.Value));

        // ActivityLogger — lightweight, but defer anyway
        services.AddSingleton<ActivityLogger>();

        // WebhookService / ErrorTrackingService — never needed at startup
        services.AddSingleton<WebhookService>(sp =>
            new WebhookService(
                sp.GetRequiredService<ConfigurationService>(),
                loggingServiceHolder.Value));
        services.AddSingleton<ErrorTrackingService>(sp =>
            new ErrorTrackingService(
                sp.GetRequiredService<ConfigurationService>(),
                loggingServiceHolder.Value));

        // Validator
        services.AddSingleton<IValidator<ProjectDto>, ProjectValidator>();

        // BusinessLogic — all 10 components deferred (none are needed at startup)
        RegisterBusinessLogic(services);

        // ProjectRepository — lazy factory
        services.AddSingleton<IProjectRepository>(sp =>
        {
            var config = sp.GetRequiredService<ConfigurationService>();
            return new ProjectRepository(config.GetProjectsDataPath());
        });

        services.AddSingleton<IProjectService, ProjectService>();

        // ── Feature Group E — Time tracking ────────────────────────────────────
        services.AddSingleton<TimeTrackingService>(sp =>
        {
            var config = sp.GetRequiredService<ConfigurationService>();
            var dataDir = Path.Combine(AppContext.BaseDirectory,
                config.Settings.ApplicationSettings.DataDirectory);
            Directory.CreateDirectory(dataDir);
            return new TimeTrackingService(Path.Combine(dataDir, "time_entries.json"));
        });

        // ── Feature Group F — Notifications ────────────────────────────────────
        services.AddSingleton<NotificationService>(sp =>
        {
            var config = sp.GetRequiredService<ConfigurationService>();
            var dataDir = Path.Combine(AppContext.BaseDirectory,
                config.Settings.ApplicationSettings.DataDirectory);
            Directory.CreateDirectory(dataDir);
            return new NotificationService(Path.Combine(dataDir, "notifications.json"));
        });

        // GitHub SQLite store — constructor returns instantly (background init)
        services.AddSingleton<IG_ReposStore>(sp =>
        {
            var config = sp.GetRequiredService<ConfigurationService>();
            var dataDir = Path.Combine(AppContext.BaseDirectory,
                config.Settings.ApplicationSettings.DataDirectory);
            Directory.CreateDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, "github.db");
            var logger  = sp.GetRequiredService<ILogger>();
            return new G_ReposStore($"Data Source={dbPath}", logger);
        });

        services.AddSingleton<IGitHubService, GitHubService>();
        services.AddSingleton<GitHubSyncWorkflow>();

        // Build — fast because no singleton is eagerly constructed here
        _serviceProvider = services.BuildServiceProvider();

        // ── Tier 3: background tasks run in parallel ──────────────────────────────

        // Kick off LoggingService setup + GitHub sync simultaneously
        Task.Run(() =>
        {
            loggingServiceHolder.Initialize(configService);
            StartAutoBackupTimer();
        });
        Task.Run(StartGitHubSync);
    }

    // Holder lets us register the lazy-initialized LoggingService in DI
    // before the real instance is ready.
    private sealed class LoggingServiceHolder
    {
        private LoggingService? _value;
        private readonly object _lock = new();

        // Returns a dummy no-op service until Initialize() completes
        public LoggingService Value
        {
            get { lock (_lock) { return _value ??= LoggingService.CreateNoop(); } }
        }

        public void Initialize(ConfigurationService config)
        {
            var real = new LoggingService(config);
            lock (_lock) { _value = real; }
        }
    }


    /// <summary>
    /// Registers all business logic components
    /// </summary>
    private static void RegisterBusinessLogic(IServiceCollection services)
    {
        // Business rules and validation - register with ProjectDto type
        services.AddSingleton<BusinessRuleEngine<ProjectDto>>();
        
        // Workflow management
        services.AddSingleton<ProjectLifecycle>();
        
        // Financial management
        services.AddSingleton<Budget_Manager>();
        
        // Resource management
        services.AddSingleton<ResourceManager>();
        
        // Milestone management
        services.AddSingleton<M_Manager>();
        
        // Approval workflows
        services.AddSingleton<AppWorkflow>();
        
        // Dependency management
        services.AddSingleton<D_manager>();
        
        // Business intelligence
        services.AddSingleton<Intel>();
        
        // Notifications and alerts
        services.AddSingleton<Notif>();
        
        // Audit trail
        services.AddSingleton<AuditTrailManager>();
    }

    /// <summary>
    /// Gets a service from the container
    /// </summary>
    public static T GetService<T>() where T : notnull
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException(
                "Service container has not been configured. Call ConfigureServices first.");
        }

        var service = _serviceProvider.GetService<T>();
        if (service == null)
        {
            throw new InvalidOperationException(
                $"Service of type {typeof(T).Name} is not registered.");
        }

        return service;
    }

    /// <summary>
    /// Gets a required service from the container (throws if not found)
    /// </summary>
    public static T GetRequiredService<T>() where T : notnull
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException(
                "Service container has not been configured. Call ConfigureServices first.");
        }

        return _serviceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Disposes the service provider and cleans up resources
    /// </summary>
    public static void Dispose()
    {
        // Stop GitHub sync workflow
        _syncWorkflow?.StopBackgroundSync();
        _syncWorkflow?.Dispose();
        _syncWorkflow = null;

        // Stop and dispose backup timer
        _backupTimer?.Dispose();
        _backupTimer = null;

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _serviceProvider = null;
    }

    /// <summary>
    /// Starts the auto-backup timer based on configuration.
    /// Fires immediately after the first interval, then repeats.
    /// </summary>
    private static void StartAutoBackupTimer()
    {
        if (_serviceProvider == null)
            return;

        var config = _serviceProvider.GetRequiredService<ConfigurationService>();
        var appSettings = config.Settings.ApplicationSettings;

        if (!appSettings.EnableAutoBackup || appSettings.BackupIntervalMinutes <= 0)
            return;

        var repository = _serviceProvider.GetRequiredService<IProjectRepository>() as Services.ProjectRepository;
        if (repository == null)
            return;

        var backupDir = config.GetBackupsDirectory();
        var maxFiles = appSettings.MaxBackupFiles;
        var intervalMs = (int)TimeSpan.FromMinutes(appSettings.BackupIntervalMinutes).TotalMilliseconds;

        _backupTimer = new System.Threading.Timer(
            callback: async _ =>
            {
                try
                {
                    await repository.CreateBackupAsync(backupDir, maxFiles);
                }
                catch
                {
                    // Backup failures are non-fatal — silently swallow to avoid crashing the timer.
                }
            },
            state: null,
            dueTime: intervalMs,   // first backup fires after one interval
            period: intervalMs);
    }
    /// <summary>
    /// Starts the GitHub background sync workflow if sync is enabled in configuration.
    /// </summary>
    private static void StartGitHubSync()
    {
        if (_serviceProvider == null) return;

        var config = _serviceProvider.GetRequiredService<ConfigurationService>();
        if (!config.Settings.GitHub.EnableSync) return;

        _syncWorkflow = _serviceProvider.GetRequiredService<Services.GitHub.GitHubSyncWorkflow>();
        _syncWorkflow.StartBackgroundSync();
    }
}
