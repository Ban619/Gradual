using System;

namespace Gradual.Models.GitHub;

/// <summary>
/// Represents a commit in a GitHub repository
/// </summary>
public class G_Commit
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to G_Repos
    /// </summary>
    public int G_ReposId { get; set; }
    
    /// <summary>
    /// Commit SHA hash
    /// </summary>
    public string Sha { get; set; } = string.Empty;
    
    /// <summary>
    /// Commit message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Author name
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;
    
    /// <summary>
    /// Author email
    /// </summary>
    public string AuthorEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Author GitHub username
    /// </summary>
    public string? AuthorUsername { get; set; }
    
    /// <summary>
    /// Committer name
    /// </summary>
    public string CommitterName { get; set; } = string.Empty;
    
    /// <summary>
    /// Committer email
    /// </summary>
    public string CommitterEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// When the commit was authored
    /// </summary>
    public DateTime AuthoredAt { get; set; }
    
    /// <summary>
    /// When the commit was committed
    /// </summary>
    public DateTime CommittedAt { get; set; }
    
    /// <summary>
    /// Commit web URL
    /// </summary>
    public string HtmlUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Parent commit SHAs (for merge commits)
    /// </summary>
    public string ParentShas { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of additions in this commit
    /// </summary>
    public int Additions { get; set; }
    
    /// <summary>
    /// Number of deletions in this commit
    /// </summary>
    public int Deletions { get; set; }
    
    /// <summary>
    /// Total changes (additions + deletions)
    /// </summary>
    public int TotalChanges => Additions + Deletions;
    
    /// <summary>
    /// Branch name this commit belongs to
    /// </summary>
    public string? BranchName { get; set; }
    
    /// <summary>
    /// Is this a merge commit?
    /// </summary>
    public bool IsMergeCommit { get; set; }
    
    /// <summary>
    /// Commit verification status (signed, unsigned, etc.)
    /// </summary>
    public string? VerificationStatus { get; set; }
    
    /// <summary>
    /// When this record was created locally
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Navigation property
    /// </summary>
    public G_Repos? Repository { get; set; }
}
