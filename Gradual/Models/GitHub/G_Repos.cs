using System;

namespace Gradual.Models.GitHub;

/// <summary>
/// Represents a GitHub repository linked to a project
/// </summary>
public class G_Repos
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to ProjectRecord
    /// </summary>
    public int ProjectId { get; set; }
    
    /// <summary>
    /// GitHub repository ID
    /// </summary>
    public long GitHubRepoId { get; set; }
    
    /// <summary>
    /// Repository owner/username
    /// </summary>
    public string Owner { get; set; } = string.Empty;
    
    /// <summary>
    /// Repository name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Full repository name (owner/name)
    /// </summary>
    public string FullName => $"{Owner}/{Name}";
    
    /// <summary>
    /// Repository description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Primary language of the repository
    /// </summary>
    public string? Language { get; set; }
    
    /// <summary>
    /// Is this a private repository?
    /// </summary>
    public bool IsPrivate { get; set; }
    
    /// <summary>
    /// Is this a fork?
    /// </summary>
    public bool IsFork { get; set; }
    
    /// <summary>
    /// Number of stars
    /// </summary>
    public int Stars { get; set; }
    
    /// <summary>
    /// Number of forks
    /// </summary>
    public int Forks { get; set; }
    
    /// <summary>
    /// Number of open issues
    /// </summary>
    public int OpenIssues { get; set; }
    
    /// <summary>
    /// Number of watchers
    /// </summary>
    public int Watchers { get; set; }
    
    /// <summary>
    /// Default branch name (usually 'main' or 'master')
    /// </summary>
    public string DefaultBranch { get; set; } = "main";
    
    /// <summary>
    /// Repository clone URL (HTTPS)
    /// </summary>
    public string CloneUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Repository web URL
    /// </summary>
    public string HtmlUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// When the repository was created on GitHub
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the repository was last updated on GitHub
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// When the repository was last pushed to
    /// </summary>
    public DateTime? PushedAt { get; set; }
    
    /// <summary>
    /// Repository size in KB
    /// </summary>
    public long Size { get; set; }
    
    /// <summary>
    /// When we last synced with GitHub
    /// </summary>
    public DateTime LastSyncedAt { get; set; }
    
    /// <summary>
    /// Sync status: Success, Failed, Pending
    /// </summary>
    public string SyncStatus { get; set; } = "Pending";
    
    /// <summary>
    /// Last sync error message if any
    /// </summary>
    public string? LastSyncError { get; set; }
    
    /// <summary>
    /// Is sync enabled for this repository?
    /// </summary>
    public bool IsSyncEnabled { get; set; } = true;
    
    /// <summary>
    /// Local record timestamp
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Navigation property to commits
    /// </summary>
    public List<G_Commit> Commits { get; set; } = new();
    
    /// <summary>
    /// Navigation property to branches
    /// </summary>
    public List<G_Branch> Branches { get; set; } = new();
    
    /// <summary>
    /// Navigation property to issues
    /// </summary>
    public List<G_Issues> Issues { get; set; } = new();
}
