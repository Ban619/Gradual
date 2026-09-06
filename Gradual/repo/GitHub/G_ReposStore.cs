using Gradual.Models.GitHub;
using Microsoft.Data.Sqlite;
using Serilog;
using System.Data.Common;

namespace Gradual.Repositories.GitHub;

/// <summary>
/// SQLite implementation of GitHub repository data store
/// </summary>
public class G_ReposStore : IG_ReposStore
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    // Lazy init: DB tables are created on the first actual use, not on construction.
    // This prevents blocking the UI thread during application startup.
    private Task? _initTask;
    private readonly object _initLock = new();

    public G_ReposStore(string connectionString, ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
        // Kick off schema creation immediately in the background so the first
        // real query doesn't wait long, but the constructor returns instantly.
        _initTask = Task.Run(async () => await InitializeDatabaseAsync());
    }

    // Awaited by every public method before touching the DB.
    private async Task EnsureInitializedAsync()
    {
        Task? t;
        lock (_initLock) { t = _initTask; }
        if (t != null) { await t; lock (_initLock) { _initTask = null; } }
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Enable WAL mode for faster concurrent reads and reduced lock contention.
            var walCmd = connection.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA cache_size=-8000; PRAGMA synchronous=NORMAL;";
            await walCmd.ExecuteNonQueryAsync();

            var createTablesCommand = connection.CreateCommand();
            createTablesCommand.CommandText = @"
                -- GitHub Repositories table
                CREATE TABLE IF NOT EXISTS GitHubRepositories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProjectId INTEGER NOT NULL,
                    GitHubRepoId INTEGER NOT NULL,
                    Owner TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    Language TEXT,
                    IsPrivate INTEGER NOT NULL,
                    IsFork INTEGER NOT NULL,
                    Stars INTEGER NOT NULL,
                    Forks INTEGER NOT NULL,
                    OpenIssues INTEGER NOT NULL,
                    Watchers INTEGER NOT NULL,
                    DefaultBranch TEXT NOT NULL,
                    CloneUrl TEXT NOT NULL,
                    HtmlUrl TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    PushedAt TEXT,
                    Size INTEGER NOT NULL,
                    LastSyncedAt TEXT NOT NULL,
                    SyncStatus TEXT NOT NULL,
                    LastSyncError TEXT,
                    IsSyncEnabled INTEGER NOT NULL,
                    CreatedDate TEXT NOT NULL,
                    UNIQUE(ProjectId),
                    UNIQUE(Owner, Name)
                );

                -- GitHub Commits table
                CREATE TABLE IF NOT EXISTS G_Commits (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    G_ReposId INTEGER NOT NULL,
                    Sha TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    AuthorName TEXT NOT NULL,
                    AuthorEmail TEXT NOT NULL,
                    AuthorUsername TEXT,
                    CommitterName TEXT NOT NULL,
                    CommitterEmail TEXT NOT NULL,
                    AuthoredAt TEXT NOT NULL,
                    CommittedAt TEXT NOT NULL,
                    HtmlUrl TEXT NOT NULL,
                    ParentShas TEXT NOT NULL,
                    Additions INTEGER NOT NULL DEFAULT 0,
                    Deletions INTEGER NOT NULL DEFAULT 0,
                    BranchName TEXT,
                    IsMergeCommit INTEGER NOT NULL,
                    VerificationStatus TEXT,
                    CreatedDate TEXT NOT NULL,
                    FOREIGN KEY(G_ReposId) REFERENCES GitHubRepositories(Id) ON DELETE CASCADE,
                    UNIQUE(G_ReposId, Sha)
                );

                -- GitHub Branches table
                CREATE TABLE IF NOT EXISTS G_Branches (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    G_ReposId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    IsDefault INTEGER NOT NULL,
                    IsProtected INTEGER NOT NULL,
                    LatestCommitSha TEXT NOT NULL,
                    LatestCommitMessage TEXT,
                    LatestCommitAuthor TEXT,
                    LatestCommitDate TEXT,
                    CommitsAhead INTEGER NOT NULL DEFAULT 0,
                    CommitsBehind INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT,
                    UpdatedAt TEXT NOT NULL,
                    LastSyncedAt TEXT NOT NULL,
                    CreatedDate TEXT NOT NULL,
                    FOREIGN KEY(G_ReposId) REFERENCES GitHubRepositories(Id) ON DELETE CASCADE,
                    UNIQUE(G_ReposId, Name)
                );

                -- GitHub Issues table
                CREATE TABLE IF NOT EXISTS G_Issuess (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    G_ReposId INTEGER NOT NULL,
                    G_IssuesId INTEGER NOT NULL,
                    Number INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    Body TEXT,
                    State TEXT NOT NULL,
                    IsPullRequest INTEGER NOT NULL,
                    CreatedBy TEXT NOT NULL,
                    AssignedTo TEXT,
                    Labels TEXT,
                    Milestone TEXT,
                    Comments INTEGER NOT NULL,
                    HtmlUrl TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    ClosedAt TEXT,
                    ClosedBy TEXT,
                    LastSyncedAt TEXT NOT NULL,
                    CreatedDate TEXT NOT NULL,
                    FOREIGN KEY(G_ReposId) REFERENCES GitHubRepositories(Id) ON DELETE CASCADE,
                    UNIQUE(G_ReposId, Number)
                );

                -- GitHub Sync Status table
                CREATE TABLE IF NOT EXISTS G_Sync (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    G_ReposId INTEGER NOT NULL,
                    SyncType TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    StartedAt TEXT NOT NULL,
                    CompletedAt TEXT,
                    DurationMs INTEGER,
                    ItemsSynced INTEGER NOT NULL,
                    ErrorMessage TEXT,
                    SyncLog TEXT,
                    IsManual INTEGER NOT NULL,
                    FOREIGN KEY(G_ReposId) REFERENCES GitHubRepositories(Id) ON DELETE CASCADE
                );

                -- Create indexes for performance
                CREATE INDEX IF NOT EXISTS idx_commits_repo ON G_Commits(G_ReposId);
                CREATE INDEX IF NOT EXISTS idx_commits_sha ON G_Commits(Sha);
                CREATE INDEX IF NOT EXISTS idx_commits_date ON G_Commits(CommittedAt);
                CREATE INDEX IF NOT EXISTS idx_branches_repo ON G_Branches(G_ReposId);
                CREATE INDEX IF NOT EXISTS idx_issues_repo ON G_Issuess(G_ReposId);
                CREATE INDEX IF NOT EXISTS idx_issues_state ON G_Issuess(State);
                CREATE INDEX IF NOT EXISTS idx_sync_repo ON G_Sync(G_ReposId);
            ";

            await createTablesCommand.ExecuteNonQueryAsync();
            _logger.Information("GitHub database tables initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize GitHub database tables");
            throw;
        }
    }

    // ── Repository operations ────────────────────────────────────────────────

    public async Task<G_Repos?> GetRepositoryByIdAsync(int id)
    {
        await EnsureInitializedAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM GitHubRepositories WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapRepository(reader) : null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get repository by ID: {Id}", id);
            return null;
        }
    }

    public async Task<G_Repos?> GetRepositoryByProjectIdAsync(int projectId)
    {
        await EnsureInitializedAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM GitHubRepositories WHERE ProjectId = @projectId";
            command.Parameters.AddWithValue("@projectId", projectId);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapRepository(reader) : null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get repository by ProjectId: {ProjectId}", projectId);
            return null;
        }
    }

    public async Task<G_Repos?> GetRepositoryByFullNameAsync(string owner, string repoName)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM GitHubRepositories WHERE Owner = @owner AND Name = @name";
            command.Parameters.AddWithValue("@owner", owner);
            command.Parameters.AddWithValue("@name", repoName);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapRepository(reader) : null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get repository by full name: {Owner}/{Repo}", owner, repoName);
            return null;
        }
    }

    public async Task<List<G_Repos>> GetAllRepositoriesAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM GitHubRepositories ORDER BY Name";

            using var reader = await command.ExecuteReaderAsync();
            var repositories = new List<G_Repos>();
            while (await reader.ReadAsync())
                repositories.Add(MapRepository(reader));

            return repositories;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get all repositories");
            return new List<G_Repos>();
        }
    }

    public async Task<List<G_Repos>> GetRepositoriesByOwnerAsync(string owner)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM GitHubRepositories WHERE Owner = @owner ORDER BY Name";
            command.Parameters.AddWithValue("@owner", owner);

            using var reader = await command.ExecuteReaderAsync();
            var repositories = new List<G_Repos>();
            while (await reader.ReadAsync())
                repositories.Add(MapRepository(reader));

            return repositories;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get repositories by owner: {Owner}", owner);
            return new List<G_Repos>();
        }
    }

    public async Task<int> AddRepositoryAsync(G_Repos repository)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO GitHubRepositories
                    (ProjectId, GitHubRepoId, Owner, Name, Description, Language, IsPrivate, IsFork,
                     Stars, Forks, OpenIssues, Watchers, DefaultBranch, CloneUrl, HtmlUrl,
                     CreatedAt, UpdatedAt, PushedAt, Size, LastSyncedAt, SyncStatus,
                     LastSyncError, IsSyncEnabled, CreatedDate)
                VALUES
                    (@ProjectId, @GitHubRepoId, @Owner, @Name, @Description, @Language, @IsPrivate, @IsFork,
                     @Stars, @Forks, @OpenIssues, @Watchers, @DefaultBranch, @CloneUrl, @HtmlUrl,
                     @CreatedAt, @UpdatedAt, @PushedAt, @Size, @LastSyncedAt, @SyncStatus,
                     @LastSyncError, @IsSyncEnabled, @CreatedDate);
                SELECT last_insert_rowid();";

            BindRepositoryParams(command, repository);

            var result = await command.ExecuteScalarAsync();
            var newId = Convert.ToInt32(result);
            _logger.Information("Added repository {Owner}/{Name} with ID {Id}", repository.Owner, repository.Name, newId);
            return newId;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add repository {Owner}/{Name}", repository.Owner, repository.Name);
            return 0;
        }
    }

    public async Task<bool> UpdateRepositoryAsync(G_Repos repository)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE GitHubRepositories SET
                    ProjectId = @ProjectId, GitHubRepoId = @GitHubRepoId,
                    Owner = @Owner, Name = @Name, Description = @Description, Language = @Language,
                    IsPrivate = @IsPrivate, IsFork = @IsFork, Stars = @Stars, Forks = @Forks,
                    OpenIssues = @OpenIssues, Watchers = @Watchers, DefaultBranch = @DefaultBranch,
                    CloneUrl = @CloneUrl, HtmlUrl = @HtmlUrl, CreatedAt = @CreatedAt,
                    UpdatedAt = @UpdatedAt, PushedAt = @PushedAt, Size = @Size,
                    LastSyncedAt = @LastSyncedAt, SyncStatus = @SyncStatus,
                    LastSyncError = @LastSyncError, IsSyncEnabled = @IsSyncEnabled
                WHERE Id = @Id";

            command.Parameters.AddWithValue("@Id", repository.Id);
            BindRepositoryParams(command, repository);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update repository {Id}", repository.Id);
            return false;
        }
    }

    public async Task<bool> DeleteRepositoryAsync(int id)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM GitHubRepositories WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            var rows = await command.ExecuteNonQueryAsync();
            _logger.Information("Deleted repository ID {Id}", id);
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete repository {Id}", id);
            return false;
        }
    }

    // ── Commit operations ────────────────────────────────────────────────────

    public async Task<List<G_Commit>> GetCommitsByRepositoryAsync(int repositoryId, int limit = 50)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM G_Commits
                WHERE G_ReposId = @repoId
                ORDER BY CommittedAt DESC
                LIMIT @limit";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@limit", limit);

            using var reader = await command.ExecuteReaderAsync();
            var commits = new List<G_Commit>();
            while (await reader.ReadAsync())
                commits.Add(MapCommit(reader));

            return commits;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get commits for repository {Id}", repositoryId);
            return new List<G_Commit>();
        }
    }

    public async Task<List<G_Commit>> GetCommitsByBranchAsync(int repositoryId, string branchName, int limit = 50)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM G_Commits
                WHERE G_ReposId = @repoId AND BranchName = @branch
                ORDER BY CommittedAt DESC
                LIMIT @limit";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@branch", branchName);
            command.Parameters.AddWithValue("@limit", limit);

            using var reader = await command.ExecuteReaderAsync();
            var commits = new List<G_Commit>();
            while (await reader.ReadAsync())
                commits.Add(MapCommit(reader));

            return commits;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get commits for branch {Branch} in repository {Id}", branchName, repositoryId);
            return new List<G_Commit>();
        }
    }

    public async Task<G_Commit?> GetCommitByShaAsync(int repositoryId, string sha)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM G_Commits WHERE G_ReposId = @repoId AND Sha = @sha";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@sha", sha);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapCommit(reader) : null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get commit {Sha}", sha);
            return null;
        }
    }

    public async Task AddCommitsAsync(List<G_Commit> commits)
    {
        if (commits.Count == 0) return;

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            foreach (var commit in commits)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT OR IGNORE INTO G_Commits
                        (G_ReposId, Sha, Message, AuthorName, AuthorEmail, AuthorUsername,
                         CommitterName, CommitterEmail, AuthoredAt, CommittedAt, HtmlUrl, ParentShas,
                         Additions, Deletions, BranchName, IsMergeCommit, VerificationStatus, CreatedDate)
                    VALUES
                        (@G_ReposId, @Sha, @Message, @AuthorName, @AuthorEmail, @AuthorUsername,
                         @CommitterName, @CommitterEmail, @AuthoredAt, @CommittedAt, @HtmlUrl, @ParentShas,
                         @Additions, @Deletions, @BranchName, @IsMergeCommit, @VerificationStatus, @CreatedDate)";

                BindCommitParams(command, commit);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            _logger.Debug("Added {Count} commits", commits.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add commits batch");
        }
    }

    public async Task<bool> CommitExistsAsync(int repositoryId, string sha)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM G_Commits WHERE G_ReposId = @repoId AND Sha = @sha";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@sha", sha);

            var count = Convert.ToInt64(await command.ExecuteScalarAsync());
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check commit existence {Sha}", sha);
            return false;
        }
    }

    public async Task<DateTime?> GetLastCommitDateAsync(int repositoryId)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(CommittedAt) FROM G_Commits WHERE G_ReposId = @repoId";
            command.Parameters.AddWithValue("@repoId", repositoryId);

            var result = await command.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return null;
            return DateTime.Parse(result.ToString()!);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get last commit date for repository {Id}", repositoryId);
            return null;
        }
    }

    // ── Branch operations ────────────────────────────────────────────────────

    public async Task<List<G_Branch>> GetBranchesByRepositoryAsync(int repositoryId)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM G_Branches
                WHERE G_ReposId = @repoId
                ORDER BY IsDefault DESC, Name";
            command.Parameters.AddWithValue("@repoId", repositoryId);

            using var reader = await command.ExecuteReaderAsync();
            var branches = new List<G_Branch>();
            while (await reader.ReadAsync())
                branches.Add(MapBranch(reader));

            return branches;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get branches for repository {Id}", repositoryId);
            return new List<G_Branch>();
        }
    }

    public async Task<G_Branch?> GetBranchByNameAsync(int repositoryId, string branchName)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM G_Branches WHERE G_ReposId = @repoId AND Name = @name";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@name", branchName);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapBranch(reader) : null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get branch {Name} for repository {Id}", branchName, repositoryId);
            return null;
        }
    }

    public async Task AddBranchesAsync(List<G_Branch> branches)
    {
        if (branches.Count == 0) return;

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            foreach (var branch in branches)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT OR IGNORE INTO G_Branches
                        (G_ReposId, Name, IsDefault, IsProtected, LatestCommitSha,
                         LatestCommitMessage, LatestCommitAuthor, LatestCommitDate,
                         CommitsAhead, CommitsBehind, CreatedAt, UpdatedAt, LastSyncedAt, CreatedDate)
                    VALUES
                        (@G_ReposId, @Name, @IsDefault, @IsProtected, @LatestCommitSha,
                         @LatestCommitMessage, @LatestCommitAuthor, @LatestCommitDate,
                         @CommitsAhead, @CommitsBehind, @CreatedAt, @UpdatedAt, @LastSyncedAt, @CreatedDate)";

                BindBranchParams(command, branch);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            _logger.Debug("Added {Count} branches", branches.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add branches batch");
        }
    }

    public async Task<bool> UpdateBranchAsync(G_Branch branch)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE G_Branches SET
                    IsDefault = @IsDefault, IsProtected = @IsProtected,
                    LatestCommitSha = @LatestCommitSha, LatestCommitMessage = @LatestCommitMessage,
                    LatestCommitAuthor = @LatestCommitAuthor, LatestCommitDate = @LatestCommitDate,
                    CommitsAhead = @CommitsAhead, CommitsBehind = @CommitsBehind,
                    UpdatedAt = @UpdatedAt, LastSyncedAt = @LastSyncedAt
                WHERE Id = @Id";

            command.Parameters.AddWithValue("@Id", branch.Id);
            BindBranchParams(command, branch);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update branch {Id}", branch.Id);
            return false;
        }
    }

    public async Task<bool> DeleteBranchAsync(int id)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM G_Branches WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete branch {Id}", id);
            return false;
        }
    }

    // ── Issue / PR operations ────────────────────────────────────────────────

    public async Task<List<G_Issues>> GetIssuesByRepositoryAsync(int repositoryId, string? state = null)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = state == null
                ? "SELECT * FROM G_Issuess WHERE G_ReposId = @repoId AND IsPullRequest = 0 ORDER BY Number DESC"
                : "SELECT * FROM G_Issuess WHERE G_ReposId = @repoId AND IsPullRequest = 0 AND State = @state ORDER BY Number DESC";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            if (state != null) command.Parameters.AddWithValue("@state", state);

            using var reader = await command.ExecuteReaderAsync();
            var issues = new List<G_Issues>();
            while (await reader.ReadAsync())
                issues.Add(MapIssue(reader));

            return issues;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get issues for repository {Id}", repositoryId);
            return new List<G_Issues>();
        }
    }

    public async Task<List<G_Issues>> GetPullRequestsByRepositoryAsync(int repositoryId, string? state = null)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = state == null
                ? "SELECT * FROM G_Issuess WHERE G_ReposId = @repoId AND IsPullRequest = 1 ORDER BY Number DESC"
                : "SELECT * FROM G_Issuess WHERE G_ReposId = @repoId AND IsPullRequest = 1 AND State = @state ORDER BY Number DESC";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            if (state != null) command.Parameters.AddWithValue("@state", state);

            using var reader = await command.ExecuteReaderAsync();
            var prs = new List<G_Issues>();
            while (await reader.ReadAsync())
                prs.Add(MapIssue(reader));

            return prs;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get pull requests for repository {Id}", repositoryId);
            return new List<G_Issues>();
        }
    }

    public async Task<G_Issues?> GetIssueByNumberAsync(int repositoryId, int issueNumber)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM G_Issuess WHERE G_ReposId = @repoId AND Number = @number";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@number", issueNumber);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapIssue(reader) : null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get issue #{Number} for repository {Id}", issueNumber, repositoryId);
            return null;
        }
    }

    public async Task AddIssuesAsync(List<G_Issues> issues)
    {
        if (issues.Count == 0) return;

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            foreach (var issue in issues)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT OR IGNORE INTO G_Issuess
                        (G_ReposId, G_IssuesId, Number, Title, Body, State, IsPullRequest,
                         CreatedBy, AssignedTo, Labels, Milestone, Comments, HtmlUrl,
                         CreatedAt, UpdatedAt, ClosedAt, ClosedBy, LastSyncedAt, CreatedDate)
                    VALUES
                        (@G_ReposId, @G_IssuesId, @Number, @Title, @Body, @State, @IsPullRequest,
                         @CreatedBy, @AssignedTo, @Labels, @Milestone, @Comments, @HtmlUrl,
                         @CreatedAt, @UpdatedAt, @ClosedAt, @ClosedBy, @LastSyncedAt, @CreatedDate)";

                BindIssueParams(command, issue);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            _logger.Debug("Added {Count} issues/PRs", issues.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add issues batch");
        }
    }

    public async Task<bool> UpdateIssueAsync(G_Issues issue)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE G_Issuess SET
                    Title = @Title, Body = @Body, State = @State,
                    AssignedTo = @AssignedTo, Labels = @Labels, Milestone = @Milestone,
                    Comments = @Comments, UpdatedAt = @UpdatedAt,
                    ClosedAt = @ClosedAt, ClosedBy = @ClosedBy, LastSyncedAt = @LastSyncedAt
                WHERE G_ReposId = @G_ReposId AND Number = @Number";

            BindIssueParams(command, issue);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update issue #{Number}", issue.Number);
            return false;
        }
    }

    public async Task<bool> IssueExistsAsync(int repositoryId, int issueNumber)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM G_Issuess WHERE G_ReposId = @repoId AND Number = @number";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@number", issueNumber);

            var count = Convert.ToInt64(await command.ExecuteScalarAsync());
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check issue existence #{Number}", issueNumber);
            return false;
        }
    }

    // ── Sync status operations ───────────────────────────────────────────────

    public async Task<List<G_Sync>> GetSyncHistoryAsync(int repositoryId, int limit = 10)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM G_Sync
                WHERE G_ReposId = @repoId
                ORDER BY StartedAt DESC
                LIMIT @limit";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@limit", limit);

            using var reader = await command.ExecuteReaderAsync();
            var history = new List<G_Sync>();
            while (await reader.ReadAsync())
                history.Add(MapSyncStatus(reader));

            return history;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get sync history for repository {Id}", repositoryId);
            return new List<G_Sync>();
        }
    }

    public async Task<G_Sync?> GetLastSyncAsync(int repositoryId)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM G_Sync
                WHERE G_ReposId = @repoId
                ORDER BY StartedAt DESC
                LIMIT 1";
            command.Parameters.AddWithValue("@repoId", repositoryId);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapSyncStatus(reader) : null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get last sync for repository {Id}", repositoryId);
            return null;
        }
    }

    public async Task<int> AddSyncStatusAsync(G_Sync syncStatus)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO G_Sync
                    (G_ReposId, SyncType, Status, StartedAt, CompletedAt,
                     DurationMs, ItemsSynced, ErrorMessage, SyncLog, IsManual)
                VALUES
                    (@G_ReposId, @SyncType, @Status, @StartedAt, @CompletedAt,
                     @DurationMs, @ItemsSynced, @ErrorMessage, @SyncLog, @IsManual);
                SELECT last_insert_rowid();";

            BindSyncStatusParams(command, syncStatus);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to add sync status record");
            return 0;
        }
    }

    public async Task<bool> UpdateSyncStatusAsync(G_Sync syncStatus)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE G_Sync SET
                    Status = @Status, CompletedAt = @CompletedAt, DurationMs = @DurationMs,
                    ItemsSynced = @ItemsSynced, ErrorMessage = @ErrorMessage, SyncLog = @SyncLog
                WHERE Id = @Id";

            command.Parameters.AddWithValue("@Id", syncStatus.Id);
            BindSyncStatusParams(command, syncStatus);

            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update sync status {Id}", syncStatus.Id);
            return false;
        }
    }

    // ── Statistics ───────────────────────────────────────────────────────────

    public async Task<int> GetTotalCommitsCountAsync(int repositoryId)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM G_Commits WHERE G_ReposId = @repoId";
            command.Parameters.AddWithValue("@repoId", repositoryId);

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get total commits count for repository {Id}", repositoryId);
            return 0;
        }
    }

    public async Task<int> GetOpenIssuesCountAsync(int repositoryId)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM G_Issuess WHERE G_ReposId = @repoId AND IsPullRequest = 0 AND State = 'open'";
            command.Parameters.AddWithValue("@repoId", repositoryId);

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get open issues count for repository {Id}", repositoryId);
            return 0;
        }
    }

    public async Task<int> GetClosedIssuesCountAsync(int repositoryId)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM G_Issuess WHERE G_ReposId = @repoId AND IsPullRequest = 0 AND State = 'closed'";
            command.Parameters.AddWithValue("@repoId", repositoryId);

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get closed issues count for repository {Id}", repositoryId);
            return 0;
        }
    }

    public async Task<Dictionary<string, int>> GetCommitsByAuthorAsync(int repositoryId)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT AuthorName, COUNT(*) AS CommitCount
                FROM G_Commits
                WHERE G_ReposId = @repoId
                GROUP BY AuthorName
                ORDER BY CommitCount DESC";
            command.Parameters.AddWithValue("@repoId", repositoryId);

            using var reader = await command.ExecuteReaderAsync();
            var result = new Dictionary<string, int>();
            while (await reader.ReadAsync())
                result[reader.GetString(0)] = reader.GetInt32(1);

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get commits by author for repository {Id}", repositoryId);
            return new Dictionary<string, int>();
        }
    }

    public async Task<List<G_Commit>> GetRecentCommitsAsync(int repositoryId, int days = 7, int limit = 20)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cutoff = DateTime.Now.AddDays(-days).ToString("O");
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM G_Commits
                WHERE G_ReposId = @repoId AND CommittedAt >= @cutoff
                ORDER BY CommittedAt DESC
                LIMIT @limit";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@cutoff", cutoff);
            command.Parameters.AddWithValue("@limit", limit);

            using var reader = await command.ExecuteReaderAsync();
            var commits = new List<G_Commit>();
            while (await reader.ReadAsync())
                commits.Add(MapCommit(reader));

            return commits;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get recent commits for repository {Id}", repositoryId);
            return new List<G_Commit>();
        }
    }

    // ── Cleanup operations ───────────────────────────────────────────────────

    public async Task<int> DeleteOldCommitsAsync(int repositoryId, DateTime beforeDate)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM G_Commits WHERE G_ReposId = @repoId AND CommittedAt < @date";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@date", beforeDate.ToString("O"));

            var rows = await command.ExecuteNonQueryAsync();
            _logger.Information("Deleted {Count} old commits from repository {Id}", rows, repositoryId);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete old commits for repository {Id}", repositoryId);
            return 0;
        }
    }

    public async Task<int> DeleteClosedIssuesAsync(int repositoryId, DateTime beforeDate)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM G_Issuess WHERE G_ReposId = @repoId AND State = 'closed' AND ClosedAt < @date";
            command.Parameters.AddWithValue("@repoId", repositoryId);
            command.Parameters.AddWithValue("@date", beforeDate.ToString("O"));

            var rows = await command.ExecuteNonQueryAsync();
            _logger.Information("Deleted {Count} closed issues from repository {Id}", rows, repositoryId);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete closed issues for repository {Id}", repositoryId);
            return 0;
        }
    }

    // ── Private helpers: parameter binding ──────────────────────────────────

    private static void BindRepositoryParams(SqliteCommand command, G_Repos r)
    {
        command.Parameters.AddWithValue("@ProjectId", r.ProjectId);
        command.Parameters.AddWithValue("@GitHubRepoId", r.GitHubRepoId);
        command.Parameters.AddWithValue("@Owner", r.Owner);
        command.Parameters.AddWithValue("@Name", r.Name);
        command.Parameters.AddWithValue("@Description", r.Description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Language", r.Language ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsPrivate", r.IsPrivate ? 1 : 0);
        command.Parameters.AddWithValue("@IsFork", r.IsFork ? 1 : 0);
        command.Parameters.AddWithValue("@Stars", r.Stars);
        command.Parameters.AddWithValue("@Forks", r.Forks);
        command.Parameters.AddWithValue("@OpenIssues", r.OpenIssues);
        command.Parameters.AddWithValue("@Watchers", r.Watchers);
        command.Parameters.AddWithValue("@DefaultBranch", r.DefaultBranch);
        command.Parameters.AddWithValue("@CloneUrl", r.CloneUrl);
        command.Parameters.AddWithValue("@HtmlUrl", r.HtmlUrl);
        command.Parameters.AddWithValue("@CreatedAt", r.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAt", r.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("@PushedAt", r.PushedAt.HasValue ? r.PushedAt.Value.ToString("O") : (object)DBNull.Value);
        command.Parameters.AddWithValue("@Size", r.Size);
        command.Parameters.AddWithValue("@LastSyncedAt", r.LastSyncedAt.ToString("O"));
        command.Parameters.AddWithValue("@SyncStatus", r.SyncStatus);
        command.Parameters.AddWithValue("@LastSyncError", r.LastSyncError ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsSyncEnabled", r.IsSyncEnabled ? 1 : 0);
        command.Parameters.AddWithValue("@CreatedDate", r.CreatedDate.ToString("O"));
    }

    private static void BindCommitParams(SqliteCommand command, G_Commit c)
    {
        command.Parameters.AddWithValue("@G_ReposId", c.G_ReposId);
        command.Parameters.AddWithValue("@Sha", c.Sha);
        command.Parameters.AddWithValue("@Message", c.Message);
        command.Parameters.AddWithValue("@AuthorName", c.AuthorName);
        command.Parameters.AddWithValue("@AuthorEmail", c.AuthorEmail);
        command.Parameters.AddWithValue("@AuthorUsername", c.AuthorUsername ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CommitterName", c.CommitterName);
        command.Parameters.AddWithValue("@CommitterEmail", c.CommitterEmail);
        command.Parameters.AddWithValue("@AuthoredAt", c.AuthoredAt.ToString("O"));
        command.Parameters.AddWithValue("@CommittedAt", c.CommittedAt.ToString("O"));
        command.Parameters.AddWithValue("@HtmlUrl", c.HtmlUrl);
        command.Parameters.AddWithValue("@ParentShas", c.ParentShas);
        command.Parameters.AddWithValue("@Additions", c.Additions);
        command.Parameters.AddWithValue("@Deletions", c.Deletions);
        command.Parameters.AddWithValue("@BranchName", c.BranchName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsMergeCommit", c.IsMergeCommit ? 1 : 0);
        command.Parameters.AddWithValue("@VerificationStatus", c.VerificationStatus ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CreatedDate", c.CreatedDate.ToString("O"));
    }

    private static void BindBranchParams(SqliteCommand command, G_Branch b)
    {
        command.Parameters.AddWithValue("@G_ReposId", b.G_ReposId);
        command.Parameters.AddWithValue("@Name", b.Name);
        command.Parameters.AddWithValue("@IsDefault", b.IsDefault ? 1 : 0);
        command.Parameters.AddWithValue("@IsProtected", b.IsProtected ? 1 : 0);
        command.Parameters.AddWithValue("@LatestCommitSha", b.LatestCommitSha);
        command.Parameters.AddWithValue("@LatestCommitMessage", b.LatestCommitMessage ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LatestCommitAuthor", b.LatestCommitAuthor ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LatestCommitDate", b.LatestCommitDate.HasValue ? b.LatestCommitDate.Value.ToString("O") : (object)DBNull.Value);
        command.Parameters.AddWithValue("@CommitsAhead", b.CommitsAhead);
        command.Parameters.AddWithValue("@CommitsBehind", b.CommitsBehind);
        command.Parameters.AddWithValue("@CreatedAt", b.CreatedAt.HasValue ? b.CreatedAt.Value.ToString("O") : (object)DBNull.Value);
        command.Parameters.AddWithValue("@UpdatedAt", b.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("@LastSyncedAt", b.LastSyncedAt.ToString("O"));
        command.Parameters.AddWithValue("@CreatedDate", b.CreatedDate.ToString("O"));
    }

    private static void BindIssueParams(SqliteCommand command, G_Issues i)
    {
        command.Parameters.AddWithValue("@G_ReposId", i.G_ReposId);
        command.Parameters.AddWithValue("@G_IssuesId", i.G_IssuesId);
        command.Parameters.AddWithValue("@Number", i.Number);
        command.Parameters.AddWithValue("@Title", i.Title);
        command.Parameters.AddWithValue("@Body", i.Body ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@State", i.State);
        command.Parameters.AddWithValue("@IsPullRequest", i.IsPullRequest ? 1 : 0);
        command.Parameters.AddWithValue("@CreatedBy", i.CreatedBy);
        command.Parameters.AddWithValue("@AssignedTo", i.AssignedTo ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Labels", i.Labels ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Milestone", i.Milestone ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Comments", i.Comments);
        command.Parameters.AddWithValue("@HtmlUrl", i.HtmlUrl);
        command.Parameters.AddWithValue("@CreatedAt", i.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAt", i.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("@ClosedAt", i.ClosedAt.HasValue ? i.ClosedAt.Value.ToString("O") : (object)DBNull.Value);
        command.Parameters.AddWithValue("@ClosedBy", i.ClosedBy ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@LastSyncedAt", i.LastSyncedAt.ToString("O"));
        command.Parameters.AddWithValue("@CreatedDate", i.CreatedDate.ToString("O"));
    }

    private static void BindSyncStatusParams(SqliteCommand command, G_Sync s)
    {
        command.Parameters.AddWithValue("@G_ReposId", s.G_ReposId);
        command.Parameters.AddWithValue("@SyncType", s.SyncType);
        command.Parameters.AddWithValue("@Status", s.Status);
        command.Parameters.AddWithValue("@StartedAt", s.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("@CompletedAt", s.CompletedAt.HasValue ? s.CompletedAt.Value.ToString("O") : (object)DBNull.Value);
        command.Parameters.AddWithValue("@DurationMs", s.DurationMs.HasValue ? s.DurationMs.Value : (object)DBNull.Value);
        command.Parameters.AddWithValue("@ItemsSynced", s.ItemsSynced);
        command.Parameters.AddWithValue("@ErrorMessage", s.ErrorMessage ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SyncLog", s.SyncLog ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsManual", s.IsManual ? 1 : 0);
    }

    // ── Private helpers: mapping ─────────────────────────────────────────────

    private static G_Repos MapRepository(DbDataReader reader)
    {
        return new G_Repos
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            ProjectId = reader.GetInt32(reader.GetOrdinal("ProjectId")),
            Owner = reader.GetString(reader.GetOrdinal("Owner")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Language = reader.IsDBNull(reader.GetOrdinal("Language")) ? null : reader.GetString(reader.GetOrdinal("Language")),
            IsPrivate = reader.GetBoolean(reader.GetOrdinal("IsPrivate")),
            IsFork = reader.GetBoolean(reader.GetOrdinal("IsFork")),
            Stars = reader.GetInt32(reader.GetOrdinal("Stars")),
            Forks = reader.GetInt32(reader.GetOrdinal("Forks")),
            OpenIssues = reader.GetInt32(reader.GetOrdinal("OpenIssues")),
            Watchers = reader.GetInt32(reader.GetOrdinal("Watchers")),
            DefaultBranch = reader.GetString(reader.GetOrdinal("DefaultBranch")),
            CloneUrl = reader.GetString(reader.GetOrdinal("CloneUrl")),
            HtmlUrl = reader.GetString(reader.GetOrdinal("HtmlUrl")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
            PushedAt = reader.IsDBNull(reader.GetOrdinal("PushedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("PushedAt"))),
            Size = reader.GetInt32(reader.GetOrdinal("Size")),
            LastSyncedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastSyncedAt"))),
            SyncStatus = reader.GetString(reader.GetOrdinal("SyncStatus")),
            LastSyncError = reader.IsDBNull(reader.GetOrdinal("LastSyncError")) ? null : reader.GetString(reader.GetOrdinal("LastSyncError")),
            IsSyncEnabled = reader.GetBoolean(reader.GetOrdinal("IsSyncEnabled"))
        };
    }

    private static G_Commit MapCommit(DbDataReader reader)
    {
        return new G_Commit
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            G_ReposId = reader.GetInt32(reader.GetOrdinal("G_ReposId")),
            Sha = reader.GetString(reader.GetOrdinal("Sha")),
            Message = reader.GetString(reader.GetOrdinal("Message")),
            AuthorName = reader.GetString(reader.GetOrdinal("AuthorName")),
            AuthorEmail = reader.GetString(reader.GetOrdinal("AuthorEmail")),
            AuthorUsername = reader.IsDBNull(reader.GetOrdinal("AuthorUsername")) ? null : reader.GetString(reader.GetOrdinal("AuthorUsername")),
            CommitterName = reader.GetString(reader.GetOrdinal("CommitterName")),
            CommitterEmail = reader.GetString(reader.GetOrdinal("CommitterEmail")),
            AuthoredAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("AuthoredAt"))),
            CommittedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CommittedAt"))),
            HtmlUrl = reader.GetString(reader.GetOrdinal("HtmlUrl")),
            ParentShas = reader.GetString(reader.GetOrdinal("ParentShas")),
            Additions = reader.GetInt32(reader.GetOrdinal("Additions")),
            Deletions = reader.GetInt32(reader.GetOrdinal("Deletions")),
            BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? null : reader.GetString(reader.GetOrdinal("BranchName")),
            IsMergeCommit = reader.GetBoolean(reader.GetOrdinal("IsMergeCommit")),
            VerificationStatus = reader.IsDBNull(reader.GetOrdinal("VerificationStatus")) ? null : reader.GetString(reader.GetOrdinal("VerificationStatus")),
            CreatedDate = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedDate")))
        };
    }

    private static G_Branch MapBranch(DbDataReader reader)
    {
        return new G_Branch
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            G_ReposId = reader.GetInt32(reader.GetOrdinal("G_ReposId")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            IsDefault = reader.GetBoolean(reader.GetOrdinal("IsDefault")),
            IsProtected = reader.GetBoolean(reader.GetOrdinal("IsProtected")),
            LatestCommitSha = reader.GetString(reader.GetOrdinal("LatestCommitSha")),
            LatestCommitMessage = reader.IsDBNull(reader.GetOrdinal("LatestCommitMessage")) ? null : reader.GetString(reader.GetOrdinal("LatestCommitMessage")),
            LatestCommitAuthor = reader.IsDBNull(reader.GetOrdinal("LatestCommitAuthor")) ? null : reader.GetString(reader.GetOrdinal("LatestCommitAuthor")),
            LatestCommitDate = reader.IsDBNull(reader.GetOrdinal("LatestCommitDate")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("LatestCommitDate"))),
            CommitsAhead = reader.GetInt32(reader.GetOrdinal("CommitsAhead")),
            CommitsBehind = reader.GetInt32(reader.GetOrdinal("CommitsBehind")),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
            LastSyncedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastSyncedAt"))),
            CreatedDate = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedDate")))
        };
    }

    private static G_Issues MapIssue(DbDataReader reader)
    {
        return new G_Issues
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            G_ReposId = reader.GetInt32(reader.GetOrdinal("G_ReposId")),
            G_IssuesId = reader.GetInt64(reader.GetOrdinal("G_IssuesId")),
            Number = reader.GetInt32(reader.GetOrdinal("Number")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Body = reader.IsDBNull(reader.GetOrdinal("Body")) ? null : reader.GetString(reader.GetOrdinal("Body")),
            State = reader.GetString(reader.GetOrdinal("State")),
            IsPullRequest = reader.GetBoolean(reader.GetOrdinal("IsPullRequest")),
            CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy")),
            AssignedTo = reader.IsDBNull(reader.GetOrdinal("AssignedTo")) ? null : reader.GetString(reader.GetOrdinal("AssignedTo")),
            Labels = reader.IsDBNull(reader.GetOrdinal("Labels")) ? null : reader.GetString(reader.GetOrdinal("Labels")),
            Milestone = reader.IsDBNull(reader.GetOrdinal("Milestone")) ? null : reader.GetString(reader.GetOrdinal("Milestone")),
            Comments = reader.GetInt32(reader.GetOrdinal("Comments")),
            HtmlUrl = reader.GetString(reader.GetOrdinal("HtmlUrl")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
            ClosedAt = reader.IsDBNull(reader.GetOrdinal("ClosedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("ClosedAt"))),
            ClosedBy = reader.IsDBNull(reader.GetOrdinal("ClosedBy")) ? null : reader.GetString(reader.GetOrdinal("ClosedBy")),
            LastSyncedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("LastSyncedAt"))),
            CreatedDate = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedDate")))
        };
    }

    private static G_Sync MapSyncStatus(DbDataReader reader)
    {
        return new G_Sync
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            G_ReposId = reader.GetInt32(reader.GetOrdinal("G_ReposId")),
            SyncType = reader.GetString(reader.GetOrdinal("SyncType")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            StartedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("StartedAt"))),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("CompletedAt"))),
            DurationMs = reader.IsDBNull(reader.GetOrdinal("DurationMs")) ? null : reader.GetInt64(reader.GetOrdinal("DurationMs")),
            ItemsSynced = reader.GetInt32(reader.GetOrdinal("ItemsSynced")),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage")) ? null : reader.GetString(reader.GetOrdinal("ErrorMessage")),
            SyncLog = reader.IsDBNull(reader.GetOrdinal("SyncLog")) ? null : reader.GetString(reader.GetOrdinal("SyncLog")),
            IsManual = reader.GetBoolean(reader.GetOrdinal("IsManual"))
        };
    }
}
