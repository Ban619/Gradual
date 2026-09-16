using Gradual.Models.GitHub;

namespace Gradual.Repositories.GitHub;

/// <summary>
/// Repository interface for GitHub data persistence
/// </summary>
public interface IG_ReposStore
{
    // Repository CRUD operations
    Task<G_Repos?> GetRepositoryByIdAsync(int id);
    Task<G_Repos?> GetRepositoryByProjectIdAsync(int projectId);
    Task<G_Repos?> GetRepositoryByFullNameAsync(string owner, string repoName);
    Task<List<G_Repos>> GetAllRepositoriesAsync();
    Task<List<G_Repos>> GetRepositoriesByOwnerAsync(string owner);
    Task<int> AddRepositoryAsync(G_Repos repository);
    Task<bool> UpdateRepositoryAsync(G_Repos repository);
    Task<bool> DeleteRepositoryAsync(int id);
    
    // Commit operations
    Task<List<G_Commit>> GetCommitsByRepositoryAsync(int repositoryId, int limit = 50);
    Task<List<G_Commit>> GetCommitsByBranchAsync(int repositoryId, string branchName, int limit = 50);
    Task<G_Commit?> GetCommitByShaAsync(int repositoryId, string sha);
    Task AddCommitsAsync(List<G_Commit> commits);
    Task<bool> CommitExistsAsync(int repositoryId, string sha);
    Task<DateTime?> GetLastCommitDateAsync(int repositoryId);
    
    // Branch operations
    Task<List<G_Branch>> GetBranchesByRepositoryAsync(int repositoryId);
    Task<G_Branch?> GetBranchByNameAsync(int repositoryId, string branchName);
    Task AddBranchesAsync(List<G_Branch> branches);
    Task<bool> UpdateBranchAsync(G_Branch branch);
    Task<bool> DeleteBranchAsync(int id);
    
    // Issue operations
    Task<List<G_Issues>> GetIssuesByRepositoryAsync(int repositoryId, string? state = null);
    Task<List<G_Issues>> GetPullRequestsByRepositoryAsync(int repositoryId, string? state = null);
    Task<G_Issues?> GetIssueByNumberAsync(int repositoryId, int issueNumber);
    Task AddIssuesAsync(List<G_Issues> issues);
    Task<bool> UpdateIssueAsync(G_Issues issue);
    Task<bool> IssueExistsAsync(int repositoryId, int issueNumber);
    
    // Sync status operations
    Task<List<G_Sync>> GetSyncHistoryAsync(int repositoryId, int limit = 10);
    Task<G_Sync?> GetLastSyncAsync(int repositoryId);
    Task<int> AddSyncStatusAsync(G_Sync syncStatus);
    Task<bool> UpdateSyncStatusAsync(G_Sync syncStatus);
    
    // Statistics and queries
    Task<int> GetTotalCommitsCountAsync(int repositoryId);
    Task<int> GetOpenIssuesCountAsync(int repositoryId);
    Task<int> GetClosedIssuesCountAsync(int repositoryId);
    Task<Dictionary<string, int>> GetCommitsByAuthorAsync(int repositoryId);
    Task<List<G_Commit>> GetRecentCommitsAsync(int repositoryId, int days = 7, int limit = 20);
    
    // Cleanup operations
    Task<int> DeleteOldCommitsAsync(int repositoryId, DateTime beforeDate);
    Task<int> DeleteClosedIssuesAsync(int repositoryId, DateTime beforeDate);
}
