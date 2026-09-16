using System;

namespace Gradual.Models.GitHub;

/// <summary>
/// Tracks GitHub sync operations and history
/// </summary>
public class G_Sync
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to G_Repos
    /// </summary>
    public int G_ReposId { get; set; }
    
    /// <summary>
    /// Type of sync: Full, Commits, Branches, Issues
    /// </summary>
    public string SyncType { get; set; } = "Full";
    
    /// <summary>
    /// Sync status: Started, InProgress, Completed, Failed
    /// </summary>
    public string Status { get; set; } = "Started";
    
    /// <summary>
    /// When the sync started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// When the sync completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }
    
    /// <summary>
    /// Number of items synced (commits, issues, etc.)
    /// </summary>
    public int ItemsSynced { get; set; }
    
    /// <summary>
    /// Error message if sync failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Detailed sync log
    /// </summary>
    public string? SyncLog { get; set; }
    
    /// <summary>
    /// Was this a manual sync or automatic?
    /// </summary>
    public bool IsManual { get; set; }
    
    /// <summary>
    /// Navigation property
    /// </summary>
    public G_Repos? Repository { get; set; }
}
