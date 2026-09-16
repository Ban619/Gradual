using System;

namespace Gradual.Models.GitHub;

/// <summary>
/// Represents a branch in a GitHub repository
/// </summary>
public class G_Branch
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to G_Repos
    /// </summary>
    public int G_ReposId { get; set; }
    
    /// <summary>
    /// Branch name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Is this the default branch?
    /// </summary>
    public bool IsDefault { get; set; }
    
    /// <summary>
    /// Is this branch protected?
    /// </summary>
    public bool IsProtected { get; set; }
    
    /// <summary>
    /// SHA of the latest commit on this branch
    /// </summary>
    public string LatestCommitSha { get; set; } = string.Empty;
    
    /// <summary>
    /// Message of the latest commit
    /// </summary>
    public string? LatestCommitMessage { get; set; }
    
    /// <summary>
    /// Author of the latest commit
    /// </summary>
    public string? LatestCommitAuthor { get; set; }
    
    /// <summary>
    /// When the latest commit was made
    /// </summary>
    public DateTime? LatestCommitDate { get; set; }
    
    /// <summary>
    /// Number of commits ahead of default branch
    /// </summary>
    public int CommitsAhead { get; set; }
    
    /// <summary>
    /// Number of commits behind default branch
    /// </summary>
    public int CommitsBehind { get; set; }
    
    /// <summary>
    /// When this branch was created
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    
    /// <summary>
    /// When this branch was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// When we last synced this branch
    /// </summary>
    public DateTime LastSyncedAt { get; set; }
    
    /// <summary>
    /// When this record was created locally
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Navigation property
    /// </summary>
    public G_Repos? Repository { get; set; }
}
