using Gradual.Models.GitHub;
using Gradual.Services;
using Microsoft.Extensions.Configuration;
using Octokit;
using Serilog;

// Alias to resolve the name collision between our model and Octokit's type.
using AppCommit = Gradual.Models.GitHub.G_Commit;

namespace Gradual.Services.GitHub;

/// <summary>
/// GitHub API integration service using Octokit
/// </summary>
public class GitHubService : IGitHubService
{
    private readonly GitHubClient _client;
    private readonly IConfiguration _configuration;
    private readonly ConfigurationService _configService;
    private readonly ILogger _logger;

    public GitHubService(IConfiguration configuration, ConfigurationService configService, ILogger logger)
    {
        _configuration = configuration;
        _configService = configService;
        _logger = logger;

        var applicationName = _configuration["GitHub:ApplicationName"] ?? "Gradual";

        // Initialize GitHub client
        _client = new GitHubClient(new ProductHeaderValue(applicationName));

        // Authenticate — env-var takes priority over appsettings via ConfigurationService
        ApplyCredentials();
    }

    /// <summary>
    /// Re-applies credentials from the current configuration (call after the user saves a new PAT).
    /// </summary>
    public void RefreshCredentials()
    {
        ApplyCredentials();
        _logger.Information("GitHub client credentials refreshed");
    }

    private void ApplyCredentials()
    {
        var pat = _configService.GetGitHubPat();
        if (!string.IsNullOrEmpty(pat))
        {
            _client.Credentials = new Credentials(pat);
            _logger.Information("GitHub client initialised with authentication");
        }
        else
        {
            _client.Credentials = Credentials.Anonymous;
            _logger.Warning("GitHub client initialised without authentication (rate limits apply)");
        }
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync()
    {
        try
        {
            var user = await _client.User.Current();
            _logger.Information("GitHub connection test successful - User: {Username}", user.Login);
            return (true, $"Connected as {user.Login}");
        }
        catch (AuthorizationException)
        {
            _logger.Error("GitHub authentication failed - invalid token");
            return (false, "Authentication failed. Check your Personal Access Token.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "GitHub connection test failed");
            return (false, $"Connection failed: {ex.Message}");
        }
    }

    public async Task<G_Repos?> GetRepositoryInfoAsync(string owner, string repoName)
    {
        try
        {
            var repo = await _client.Repository.Get(owner, repoName);
            
            return new G_Repos
            {
                GitHubRepoId = repo.Id,
                Owner = repo.Owner.Login,
                Name = repo.Name,
                Description = repo.Description,
                Language = repo.Language,
                IsPrivate = repo.Private,
                IsFork = repo.Fork,
                Stars = repo.StargazersCount,
                Forks = repo.ForksCount,
                OpenIssues = repo.OpenIssuesCount,
                Watchers = repo.SubscribersCount,
                DefaultBranch = repo.DefaultBranch,
                CloneUrl = repo.CloneUrl,
                HtmlUrl = repo.HtmlUrl,
                CreatedAt = repo.CreatedAt.DateTime,
                UpdatedAt = repo.UpdatedAt.DateTime,
                PushedAt = repo.PushedAt?.DateTime,
                Size = repo.Size,
                LastSyncedAt = DateTime.Now,
                SyncStatus = "Success"
            };
        }
        catch (NotFoundException)
        {
            _logger.Warning("Repository not found: {Owner}/{Repo}", owner, repoName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get repository info for {Owner}/{Repo}", owner, repoName);
            return null;
        }
    }

    public async Task<List<AppCommit>> GetCommitsAsync(string owner, string repoName, int count = 50)
    {
        try
        {
            var request = new CommitRequest
            {
                Since = DateTime.Now.AddMonths(-6) // Last 6 months
            };
            
            var commits = await _client.Repository.Commit.GetAll(owner, repoName, request);
            
            return commits.Take(count).Select(c => new AppCommit
            {
                Sha = c.Sha,
                Message = c.Commit.Message,
                AuthorName = c.Commit.Author.Name,
                AuthorEmail = c.Commit.Author.Email,
                AuthorUsername = c.Author?.Login,
                CommitterName = c.Commit.Committer.Name,
                CommitterEmail = c.Commit.Committer.Email,
                AuthoredAt = c.Commit.Author.Date.DateTime,
                CommittedAt = c.Commit.Committer.Date.DateTime,
                HtmlUrl = c.HtmlUrl,
                ParentShas = string.Join(",", c.Parents.Select(p => p.Sha)),
                IsMergeCommit = c.Parents.Count > 1,
                VerificationStatus = c.Commit.Verification?.Verified == true ? "Verified" : "Unverified",
                CreatedDate = DateTime.Now
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get commits for {Owner}/{Repo}", owner, repoName);
            return new List<AppCommit>();
        }
    }

    public async Task<List<AppCommit>> GetCommitsByBranchAsync(string owner, string repoName, string branchName, int count = 50)
    {
        try
        {
            var request = new CommitRequest
            {
                Sha = branchName,
                Since = DateTime.Now.AddMonths(-3)
            };
            
            var commits = await _client.Repository.Commit.GetAll(owner, repoName, request);
            
            return commits.Take(count).Select(c => new AppCommit
            {
                Sha = c.Sha,
                Message = c.Commit.Message,
                AuthorName = c.Commit.Author.Name,
                AuthorEmail = c.Commit.Author.Email,
                AuthorUsername = c.Author?.Login,
                CommitterName = c.Commit.Committer.Name,
                CommitterEmail = c.Commit.Committer.Email,
                AuthoredAt = c.Commit.Author.Date.DateTime,
                CommittedAt = c.Commit.Committer.Date.DateTime,
                HtmlUrl = c.HtmlUrl,
                BranchName = branchName,
                ParentShas = string.Join(",", c.Parents.Select(p => p.Sha)),
                IsMergeCommit = c.Parents.Count > 1,
                CreatedDate = DateTime.Now
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get commits for branch {Branch} in {Owner}/{Repo}", branchName, owner, repoName);
            return new List<AppCommit>();
        }
    }

    public async Task<List<G_Branch>> GetBranchesAsync(string owner, string repoName)
    {
        try
        {
            var repo = await _client.Repository.Get(owner, repoName);
            var branches = await _client.Repository.Branch.GetAll(owner, repoName);

            // In Octokit v13, Branch.Commit is a GitReference containing only .Sha.
            // Fetching full commit details per-branch would consume extra API quota,
            // so message/author/date are left empty and populated on demand.
            return branches.Select(b => new G_Branch
            {
                Name = b.Name,
                IsDefault = b.Name == repo.DefaultBranch,
                IsProtected = b.Protected,
                LatestCommitSha = b.Commit?.Sha ?? string.Empty,
                LatestCommitMessage = string.Empty,
                LatestCommitAuthor = string.Empty,
                LatestCommitDate = null,
                UpdatedAt = DateTime.Now,
                LastSyncedAt = DateTime.Now,
                CreatedDate = DateTime.Now
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get branches for {Owner}/{Repo}", owner, repoName);
            return new List<G_Branch>();
        }
    }

    public async Task<List<G_Issues>> GetIssuesAsync(string owner, string repoName, string state = "all")
    {
        try
        {
            var issueRequest = new RepositoryIssueRequest
            {
                State = state.ToLower() == "open" ? ItemStateFilter.Open : 
                        state.ToLower() == "closed" ? ItemStateFilter.Closed : 
                        ItemStateFilter.All
            };
            
            var issues = await _client.Issue.GetAllForRepository(owner, repoName, issueRequest);
            
            return issues.Where(i => i.PullRequest == null).Select(i => new G_Issues
            {
                G_IssuesId = i.Id,
                Number = i.Number,
                Title = i.Title,
                Body = i.Body,
                State = i.State.StringValue,
                IsPullRequest = false,
                CreatedBy = i.User.Login,
                AssignedTo = i.Assignee?.Login,
                Labels = string.Join(", ", i.Labels.Select(l => l.Name)),
                Milestone = i.Milestone?.Title,
                Comments = i.Comments,
                HtmlUrl = i.HtmlUrl,
                CreatedAt = i.CreatedAt.DateTime,
                UpdatedAt = i.UpdatedAt.GetValueOrDefault().DateTime,
                ClosedAt = i.ClosedAt?.DateTime,
                ClosedBy = i.ClosedBy?.Login,
                LastSyncedAt = DateTime.Now,
                CreatedDate = DateTime.Now
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get issues for {Owner}/{Repo}", owner, repoName);
            return new List<G_Issues>();
        }
    }

    public async Task<List<G_Issues>> GetPullRequestsAsync(string owner, string repoName, string state = "all")
    {
        try
        {
            var prRequest = new PullRequestRequest
            {
                State = state.ToLower() == "open" ? ItemStateFilter.Open : 
                        state.ToLower() == "closed" ? ItemStateFilter.Closed : 
                        ItemStateFilter.All
            };
            
            var pullRequests = await _client.PullRequest.GetAllForRepository(owner, repoName, prRequest);
            
            return pullRequests.Select(pr => new G_Issues
            {
                G_IssuesId = pr.Id,
                Number = pr.Number,
                Title = pr.Title,
                Body = pr.Body,
                State = pr.State.StringValue,
                IsPullRequest = true,
                CreatedBy = pr.User.Login,
                AssignedTo = pr.Assignee?.Login,
                Labels = string.Join(", ", pr.Labels.Select(l => l.Name)),
                Milestone = pr.Milestone?.Title,
                Comments = pr.Comments,
                HtmlUrl = pr.HtmlUrl,
                CreatedAt = pr.CreatedAt.DateTime,
                UpdatedAt = pr.UpdatedAt.DateTime,
                ClosedAt = pr.ClosedAt?.DateTime,
                ClosedBy = pr.MergedBy?.Login,
                LastSyncedAt = DateTime.Now,
                CreatedDate = DateTime.Now
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get pull requests for {Owner}/{Repo}", owner, repoName);
            return new List<G_Issues>();
        }
    }

    public async Task<G_ReposStats?> GetRepositoryStatsAsync(string owner, string repoName)
    {
        try
        {
            // Get languages
            var languages = await _client.Repository.GetAllLanguages(owner, repoName);
            
            // Get contributors
            var contributors = await _client.Repository.GetAllContributors(owner, repoName);
            
            // Get commit activity (last year)
            var commits = await _client.Repository.Commit.GetAll(owner, repoName, new CommitRequest 
            { 
                Since = DateTime.Now.AddYears(-1) 
            });
            
            return new G_ReposStats
            {
                Languages = languages.ToDictionary(l => l.Name, l => l.NumberOfBytes),
                TotalCommits = commits.Count,
                Contributors = contributors.Count,
                TopContributors = contributors.Take(5).Select(c => new GitHubContributor
                {
                    Username = c.Login,
                    AvatarUrl = c.AvatarUrl,
                    Contributions = c.Contributions
                }).ToList(),
                LastActivityDate = commits.Any() ? commits.Max(c => c.Commit.Author.Date.DateTime) : null
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get repository stats for {Owner}/{Repo}", owner, repoName);
            return null;
        }
    }

    public async Task<List<G_Repos>> SearchRepositoriesAsync(string query, int count = 10)
    {
        try
        {
            var searchRequest = new SearchRepositoriesRequest(query)
            {
                PerPage = count
            };
            
            var searchResult = await _client.Search.SearchRepo(searchRequest);
            
            return searchResult.Items.Select(repo => new G_Repos
            {
                GitHubRepoId = repo.Id,
                Owner = repo.Owner.Login,
                Name = repo.Name,
                Description = repo.Description,
                Language = repo.Language,
                IsPrivate = repo.Private,
                IsFork = repo.Fork,
                Stars = repo.StargazersCount,
                Forks = repo.ForksCount,
                OpenIssues = repo.OpenIssuesCount,
                Watchers = repo.SubscribersCount,
                DefaultBranch = repo.DefaultBranch,
                CloneUrl = repo.CloneUrl,
                HtmlUrl = repo.HtmlUrl,
                CreatedAt = repo.CreatedAt.DateTime,
                UpdatedAt = repo.UpdatedAt.DateTime,
                PushedAt = repo.PushedAt?.DateTime,
                Size = repo.Size,
                LastSyncedAt = DateTime.Now
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to search repositories with query: {Query}", query);
            return new List<G_Repos>();
        }
    }

    public async Task<List<G_Repos>> GetUserRepositoriesAsync(int count = 100)
    {
        try
        {
            var repos = await _client.Repository.GetAllForCurrent(new RepositoryRequest
            {
                Type = RepositoryType.All,
                Sort = RepositorySort.Updated
            });
            
            return repos.Take(count).Select(repo => new G_Repos
            {
                GitHubRepoId = repo.Id,
                Owner = repo.Owner.Login,
                Name = repo.Name,
                Description = repo.Description,
                Language = repo.Language,
                IsPrivate = repo.Private,
                IsFork = repo.Fork,
                Stars = repo.StargazersCount,
                Forks = repo.ForksCount,
                OpenIssues = repo.OpenIssuesCount,
                Watchers = repo.SubscribersCount,
                DefaultBranch = repo.DefaultBranch,
                CloneUrl = repo.CloneUrl,
                HtmlUrl = repo.HtmlUrl,
                CreatedAt = repo.CreatedAt.DateTime,
                UpdatedAt = repo.UpdatedAt.DateTime,
                PushedAt = repo.PushedAt?.DateTime,
                Size = repo.Size,
                LastSyncedAt = DateTime.Now,
                SyncStatus = "Success"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get user repositories");
            return new List<G_Repos>();
        }
    }

    public async Task<GitHubRateLimit> GetRateLimitAsync()
    {
        try
        {
            var rateLimit = await _client.RateLimit.GetRateLimits();
            var coreLimit = rateLimit.Rate;
            
            return new GitHubRateLimit
            {
                Limit = coreLimit.Limit,
                Remaining = coreLimit.Remaining,
                ResetAt = coreLimit.Reset.DateTime
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get rate limit information");
            return new GitHubRateLimit
            {
                Limit = 60,
                Remaining = 0,
                ResetAt = DateTime.UtcNow.AddHours(1)
            };
        }
    }
}
