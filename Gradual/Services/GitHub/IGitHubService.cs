using Gradual.Models.GitHub;

namespace Gradual.Services.GitHub;

/// <summary>
/// Interface for GitHub API integration service
/// </summary>
public interface IGitHubService
{
    /// <summary>
    /// Test connection to GitHub API
    /// </summary>
    Task<(bool Success, string Message)> TestConnectionAsync();
    
    /// <summary>
    /// Get repository information from GitHub
    /// </summary>
    Task<G_Repos?> GetRepositoryInfoAsync(string owner, string repoName);
    
    /// <summary>
    /// Get commits for a repository
    /// </summary>
    Task<List<G_Commit>> GetCommitsAsync(string owner, string repoName, int count = 50);
    
    /// <summary>
    /// Get commits for a specific branch
    /// </summary>
    Task<List<G_Commit>> GetCommitsByBranchAsync(string owner, string repoName, string branchName, int count = 50);
    
    /// <summary>
    /// Get branches for a repository
    /// </summary>
    Task<List<G_Branch>> GetBranchesAsync(string owner, string repoName);
    
    /// <summary>
    /// Get issues for a repository
    /// </summary>
    Task<List<G_Issues>> GetIssuesAsync(string owner, string repoName, string state = "all");
    
    /// <summary>
    /// Get pull requests for a repository
    /// </summary>
    Task<List<G_Issues>> GetPullRequestsAsync(string owner, string repoName, string state = "all");
    
    /// <summary>
    /// Get repository statistics (contributors, languages, etc.)
    /// </summary>
    Task<G_ReposStats?> GetRepositoryStatsAsync(string owner, string repoName);
    
    /// <summary>
    /// Search repositories by query
    /// </summary>
    Task<List<G_Repos>> SearchRepositoriesAsync(string query, int count = 10);
    
    /// <summary>
    /// Get authenticated user's repositories
    /// </summary>
    Task<List<G_Repos>> GetUserRepositoriesAsync(int count = 100);
    
    /// <summary>
    /// Get rate limit information
    /// </summary>
    Task<GitHubRateLimit> GetRateLimitAsync();
}

/// <summary>
/// Repository statistics from GitHub
/// </summary>
public class G_ReposStats
{
    public Dictionary<string, long> Languages { get; set; } = new();
    public int TotalCommits { get; set; }
    public int Contributors { get; set; }
    public List<GitHubContributor> TopContributors { get; set; } = new();
    public long CodeFrequency { get; set; }
    public DateTime? LastActivityDate { get; set; }
}

/// <summary>
/// Contributor information
/// </summary>
public class GitHubContributor
{
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int Contributions { get; set; }
}

/// <summary>
/// GitHub API rate limit status
/// </summary>
public class GitHubRateLimit
{
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public DateTime ResetAt { get; set; }
    public bool IsExceeded => Remaining == 0;
    public TimeSpan TimeUntilReset => ResetAt - DateTime.UtcNow;
}
