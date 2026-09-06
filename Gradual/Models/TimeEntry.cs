namespace Gradual.Models;

/// <summary>
/// Represents a single time log entry linked to a project (Features 41–50).
/// </summary>
public class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty; // denormalized for fast display
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Hours { get; set; } = 0m;
    public string Description { get; set; } = string.Empty;
    public bool IsBillable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Timer support — set by the floating timer form
    public DateTime? TimerStartedAt { get; set; }
    public bool IsRunning => TimerStartedAt.HasValue;

    /// <summary>Live duration when timer is active.</summary>
    public TimeSpan LiveDuration =>
        IsRunning ? DateTime.Now - TimerStartedAt!.Value : TimeSpan.FromHours((double)Hours);
}
