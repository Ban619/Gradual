using Gradual.Models.GitHub;
using Gradual.Repositories.GitHub;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Diagnostics;

namespace Gradual.Services.GitHub;

/// <summary>
/// Background service for syncing GitHub repository data
/// Handles automatic sync, manual refresh, and conflict detection
/// </summary>
public class GitHubSyncWorkflow
{
    private readonly IGitHubService _githubService;
    private readonly IG_ReposStore _repositoryStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    
    private readonly System.Threading.Timer _syncTimer;
    private readonly int _syncIntervalMinutes;
    private readonly bool _syncEnabled;
    private bool _isSyncing = false;

    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
    public event EventHandler<SyncErrorEventArgs>? SyncError;
    public event EventHandler<SyncProgressEventArgs>? SyncProgress;

    public GitHubSyncWorkflow(
        IGitHubService githubService, 
        IG_ReposStore repositoryStore,
        IConfiguration configuration,
        ILogger logger)
    {
        _githubService = githubService;
        _repositoryStore = repositoryStore;
        _configuration = configuration;
        _logger = logger;
        
        // Get configuration
        _syncEnabled = configuration.GetValue("GitHub:EnableSync", true);
        _syncIntervalMinutes = configuration.GetValue("GitHub:SyncIntervalMinutes", 30);
        
        // Initialize timer
        var intervalMs = _syncIntervalMinutes * 60 * 1000;
        _syncTimer = new System.Threading.Timer(OnTimerElapsed, null, Timeout.Infinite, intervalMs);
        
        _logger.Information("GitHubSyncWorkflow initialized. Sync enabled: {Enabled}, Interval: {Minutes} minutes", 
            _syncEnabled, _syncIntervalMinutes);
    }

    /// <summary>
    /// Starts automatic background sync
    /// </summary>
    public void StartBackgroundSync()
    {
        if (!_syncEnabled)
        {
            _logger.Warning("Background sync not started - disabled in configuration");
            return;
        }
        
        var intervalMs = _syncIntervalMinutes * 60 * 1000;
        _syncTimer.Change(intervalMs, intervalMs);
        _logger.Information("Background sync started with {Minutes} minute interval", _syncIntervalMinutes);
    }

    /// <summary>
    /// Stops automatic background sync
    /// </summary>
    public void StopBackgroundSync()
    {
        _syncTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.Information("Background sync stopped");
    }

    /// <summary>
    /// Manually triggers a sync for a specific repository
    /// </summary>
    public async Task<SyncResult> SyncRepositoryManuallyAsync(int repositoryId)
    {
        var repo = await _repositoryStore.GetRepositoryByIdAsync(repositoryId);
        if (repo == null)
        {
            return new SyncResult 
            { 
                Success = false, 
                ErrorMessage = $"Repository {repositoryId} not found" 
            };
        }
        
        return await SyncRepositoryAsync(repo, isManual: true);
    }

    /// <summary>
    /// Syncs all enabled repositories
    /// </summary>
    public async Task<List<SyncResult>> SyncAllRepositoriesAsync()
    {
        if (_isSyncing)
        {
            _logger.Warning("Sync already in progress, skipping");
            return new List<SyncResult>();
        }
        
        _isSyncing = true;
        var results = new List<SyncResult>();
        
        try
        {
            var repositories = await _repositoryStore.GetAllRepositoriesAsync();
            var enabledRepos = repositories.Where(r => r.IsSyncEnabled).ToList();
            
            _logger.Information("Starting sync for {Count} repositories", enabledRepos.Count);
            
            foreach (var repo in enabledRepos)
            {
                var result = await SyncRepositoryAsync(repo, isManual: false);
                results.Add(result);
                
                // Small delay between repositories to avoid rate limiting
                await Task.Delay(1000);
            }
            
            _logger.Information("Completed sync for {Count} repositories. Success: {Success}, Failed: {Failed}",
                results.Count,
                results.Count(r => r.Success),
                results.Count(r => !r.Success));
        }
        finally
        {
            _isSyncing = false;
        }
        
        return results;
    }

    /// <summary>
    /// Syncs a single repository
    /// </summary>
    private async Task<SyncResult> SyncRepositoryAsync(G_Repos repository, bool isManual)
    {
        var stopwatch = Stopwatch.StartNew();
        var syncStatus = new G_Sync
        {
            G_ReposId = repository.Id,
            SyncType = "Full",
            Status = "Started",
            StartedAt = DateTime.Now,
            IsManual = isManual
        };
        
        try
        {
            _logger.Information("Syncing repository: {Owner}/{Repo}", repository.Owner, repository.Name);
            OnSyncProgress(new SyncProgressEventArgs 
            { 
                RepositoryName = repository.FullName, 
                Stage = "Starting" 
            });
            
            // Create sync status record
            var syncStatusId = await _repositoryStore.AddSyncStatusAsync(syncStatus);
            
            // Step 1: Update repository info
            OnSyncProgress(new SyncProgressEventArgs 
            { 
                RepositoryName = repository.FullName, 
                Stage = "Updating repository info" 
            });
            
            var updatedRepo = await _githubService.GetRepositoryInfoAsync(repository.Owner, repository.Name);
            if (updatedRepo != null)
            {
                updatedRepo.Id = repository.Id;
                updatedRepo.ProjectId = repository.ProjectId;
                await _repositoryStore.UpdateRepositoryAsync(updatedRepo);
                repository = updatedRepo;
            }
            
            // Step 2: Sync commits
            OnSyncProgress(new SyncProgressEventArgs 
            { 
                RepositoryName = repository.FullName, 
                Stage = "Syncing commits" 
            });
            
            var commitsSynced = await SyncCommitsAsync(repository);
            
            // Step 3: Sync branches
            OnSyncProgress(new SyncProgressEventArgs 
            { 
                RepositoryName = repository.FullName, 
                Stage = "Syncing branches" 
            });
            
            var branchesSynced = await SyncBranchesAsync(repository);
            
            // Step 4: Sync issues
            OnSyncProgress(new SyncProgressEventArgs 
            { 
                RepositoryName = repository.FullName, 
                Stage = "Syncing issues" 
            });
            
            var issuesSynced = await SyncIssuesAsync(repository);
            
            // Update sync status
            stopwatch.Stop();
            syncStatus.Status = "Completed";
            syncStatus.CompletedAt = DateTime.Now;
            syncStatus.DurationMs = stopwatch.ElapsedMilliseconds;
            syncStatus.ItemsSynced = commitsSynced + branchesSynced + issuesSynced;
            syncStatus.SyncLog = $"Commits: {commitsSynced}, Branches: {branchesSynced}, Issues: {issuesSynced}";
            await _repositoryStore.UpdateSyncStatusAsync(syncStatus);
            
            // Update repository sync status
            repository.LastSyncedAt = DateTime.Now;
            repository.SyncStatus = "Success";
            repository.LastSyncError = null;
            await _repositoryStore.UpdateRepositoryAsync(repository);
            
            _logger.Information("Successfully synced {Owner}/{Repo} in {Ms}ms. Items: {Items}",
                repository.Owner, repository.Name, stopwatch.ElapsedMilliseconds, syncStatus.ItemsSynced);
            
            var result = new SyncResult
            {
                Success = true,
                RepositoryId = repository.Id,
                RepositoryName = repository.FullName,
                ItemsSynced = syncStatus.ItemsSynced,
                DurationMs = syncStatus.DurationMs.Value
            };
            
            OnSyncCompleted(new SyncCompletedEventArgs { Result = result });
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Update sync status with error
            syncStatus.Status = "Failed";
            syncStatus.CompletedAt = DateTime.Now;
            syncStatus.DurationMs = stopwatch.ElapsedMilliseconds;
            syncStatus.ErrorMessage = ex.Message;
            await _repositoryStore.UpdateSyncStatusAsync(syncStatus);
            
            // Update repository sync status
            repository.LastSyncedAt = DateTime.Now;
            repository.SyncStatus = "Failed";
            repository.LastSyncError = ex.Message;
            await _repositoryStore.UpdateRepositoryAsync(repository);
            
            _logger.Error(ex, "Failed to sync repository: {Owner}/{Repo}", repository.Owner, repository.Name);
            
            var result = new SyncResult
            {
                Success = false,
                RepositoryId = repository.Id,
                RepositoryName = repository.FullName,
                ErrorMessage = ex.Message,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
            
            OnSyncError(new SyncErrorEventArgs { Result = result, Exception = ex });
            return result;
        }
    }

    private async Task<int> SyncCommitsAsync(G_Repos repository)
    {
        try
        {
            var maxCommits = _configuration.GetValue("GitHub:MaxCommitsPerSync", 100);
            var commits = await _githubService.GetCommitsAsync(repository.Owner, repository.Name, maxCommits);
            
            var newCommits = 0;
            foreach (var commit in commits)
            {
                // Check if commit already exists
                if (!await _repositoryStore.CommitExistsAsync(repository.Id, commit.Sha))
                {
                    commit.G_ReposId = repository.Id;
                    await _repositoryStore.AddCommitsAsync(new List<G_Commit> { commit });
                    newCommits++;
                }
            }
            
            _logger.Debug("Synced {New} new commits out of {Total} fetched for {Repo}",
                newCommits, commits.Count, repository.FullName);
            
            return newCommits;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to sync commits for {Repo}", repository.FullName);
            return 0;
        }
    }

    private async Task<int> SyncBranchesAsync(G_Repos repository)
    {
        try
        {
            var branches = await _githubService.GetBranchesAsync(repository.Owner, repository.Name);
            
            foreach (var branch in branches)
            {
                branch.G_ReposId = repository.Id;
                
                var existing = await _repositoryStore.GetBranchByNameAsync(repository.Id, branch.Name);
                if (existing == null)
                {
                    await _repositoryStore.AddBranchesAsync(new List<G_Branch> { branch });
                }
                else
                {
                    branch.Id = existing.Id;
                    await _repositoryStore.UpdateBranchAsync(branch);
                }
            }
            
            _logger.Debug("Synced {Count} branches for {Repo}", branches.Count, repository.FullName);
            return branches.Count;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to sync branches for {Repo}", repository.FullName);
            return 0;
        }
    }

    private async Task<int> SyncIssuesAsync(G_Repos repository)
    {
        try
        {
            var maxIssues = _configuration.GetValue("GitHub:MaxIssuesPerSync", 50);
            
            // Get open issues
            var issues = await _githubService.GetIssuesAsync(repository.Owner, repository.Name, "open");
            issues = issues.Take(maxIssues).ToList();
            
            // Get open pull requests
            var prs = await _githubService.GetPullRequestsAsync(repository.Owner, repository.Name, "open");
            prs = prs.Take(maxIssues).ToList();
            
            var allIssues = issues.Concat(prs).ToList();
            
            foreach (var issue in allIssues)
            {
                issue.G_ReposId = repository.Id;
                
                if (!await _repositoryStore.IssueExistsAsync(repository.Id, issue.Number))
                {
                    await _repositoryStore.AddIssuesAsync(new List<G_Issues> { issue });
                }
                else
                {
                    await _repositoryStore.UpdateIssueAsync(issue);
                }
            }
            
            _logger.Debug("Synced {Count} issues/PRs for {Repo}", allIssues.Count, repository.FullName);
            return allIssues.Count;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to sync issues for {Repo}", repository.FullName);
            return 0;
        }
    }

    private async void OnTimerElapsed(object? state)
    {
        if (!_syncEnabled || _isSyncing)
        {
            return;
        }
        
        _logger.Information("Background sync triggered by timer");
        await SyncAllRepositoriesAsync();
    }

    private void OnSyncCompleted(SyncCompletedEventArgs e)
    {
        SyncCompleted?.Invoke(this, e);
    }

    private void OnSyncError(SyncErrorEventArgs e)
    {
        SyncError?.Invoke(this, e);
    }

    private void OnSyncProgress(SyncProgressEventArgs e)
    {
        SyncProgress?.Invoke(this, e);
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
    }
}

/// <summary>
/// Result of a sync operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public int RepositoryId { get; set; }
    public string RepositoryName { get; set; } = string.Empty;
    public int ItemsSynced { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Event args for sync completed
/// </summary>
public class SyncCompletedEventArgs : EventArgs
{
    public SyncResult Result { get; set; } = null!;
}

/// <summary>
/// Event args for sync error
/// </summary>
public class SyncErrorEventArgs : EventArgs
{
    public SyncResult Result { get; set; } = null!;
    public Exception Exception { get; set; } = null!;
}

/// <summary>
/// Event args for sync progress
/// </summary>
public class SyncProgressEventArgs : EventArgs
{
    public string RepositoryName { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
}
