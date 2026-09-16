using System;

namespace Gradual.Models.GitHub;

/// <summary>
/// Represents an issue or pull request in a GitHub repository
/// </summary>
public class G_Issues
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to G_Repos
    /// </summary>
    public int G_ReposId { get; set; }
    
    /// <summary>
    /// GitHub issue ID
    /// </summary>
    public long G_IssuesId { get; set; }
    
    /// <summary>
    /// Issue number
    /// </summary>
    public int Number { get; set; }
    
    /// <summary>
    /// Issue title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Issue body/description
    /// </summary>
    public string? Body { get; set; }
    
    /// <summary>
    /// Issue state: open, closed
    /// </summary>
    public string State { get; set; } = "open";
    
    /// <summary>
    /// Is this a pull request?
    /// </summary>
    public bool IsPullRequest { get; set; }
    
    /// <summary>
    /// User who created the issue
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// User assigned to the issue
    /// </summary>
    public string? AssignedTo { get; set; }
    
    /// <summary>
    /// Comma-separated labels
    /// </summary>
    public string? Labels { get; set; }
    
    /// <summary>
    /// Milestone name if any
    /// </summary>
    public string? Milestone { get; set; }
    
    /// <summary>
    /// Number of comments
    /// </summary>
    public int Comments { get; set; }
    
    /// <summary>
    /// Issue web URL
    /// </summary>
    public string HtmlUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// When the issue was created on GitHub
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the issue was last updated on GitHub
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// When the issue was closed (if closed)
    /// </summary>
    public DateTime? ClosedAt { get; set; }
    
    /// <summary>
    /// User who closed the issue
    /// </summary>
    public string? ClosedBy { get; set; }
    
    /// <summary>
    /// When we last synced this issue
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
