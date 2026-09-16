namespace Gradual.BusinessLogic.Milestones;

/// <summary>
/// Manages project milestones and deadlines
/// </summary>
public class M_Manager
{
    private readonly List<ProjectMilestone> _milestones = new();

    /// <summary>
    /// Adds a milestone to a project
    /// </summary>
    public MilestoneResult AddMilestone(Guid projectId, string name, string description, DateTime dueDate, MilestonePriority priority)
    {
        var milestone = new ProjectMilestone
        {
            MilestoneId = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Description = description,
            DueDate = dueDate,
            Priority = priority,
            Status = MilestoneStatus.NotStarted,
            CreatedDate = DateTime.Now
        };

        _milestones.Add(milestone);

        return new MilestoneResult
        {
            IsSuccess = true,
            Message = "Milestone added successfully",
            Milestone = milestone,
            DaysUntilDue = (dueDate - DateTime.Now).Days
        };
    }

    /// <summary>
    /// Updates milestone status
    /// </summary>
    public MilestoneResult UpdateMilestoneStatus(Guid milestoneId, MilestoneStatus newStatus)
    {
        var milestone = _milestones.FirstOrDefault(m => m.MilestoneId == milestoneId);
        if (milestone == null)
        {
            return MilestoneResult.Failure("Milestone not found");
        }

        var oldStatus = milestone.Status;
        milestone.Status = newStatus;

        if (newStatus == MilestoneStatus.Completed)
        {
            milestone.CompletedDate = DateTime.Now;
            milestone.IsCompleted = true;
        }

        return new MilestoneResult
        {
            IsSuccess = true,
            Message = $"Milestone status updated from {oldStatus} to {newStatus}",
            Milestone = milestone
        };
    }

    /// <summary>
    /// Checks for approaching or overdue milestones
    /// </summary>
    public List<MilestoneAlert> GetMilestoneAlerts(Guid projectId)
    {
        var alerts = new List<MilestoneAlert>();
        var projectMilestones = _milestones.Where(m => m.ProjectId == projectId && !m.IsCompleted);

        foreach (var milestone in projectMilestones)
        {
            var daysUntilDue = (milestone.DueDate - DateTime.Now).Days;

            if (daysUntilDue < 0)
            {
                alerts.Add(new MilestoneAlert
                {
                    Milestone = milestone,
                    AlertType = AlertType.Overdue,
                    Severity = AlertSeverity.Critical,
                    Message = $"OVERDUE: '{milestone.Name}' was due {Math.Abs(daysUntilDue)} days ago",
                    DaysFromDue = daysUntilDue
                });
            }
            else if (daysUntilDue == 0)
            {
                alerts.Add(new MilestoneAlert
                {
                    Milestone = milestone,
                    AlertType = AlertType.DueToday,
                    Severity = AlertSeverity.High,
                    Message = $"DUE TODAY: '{milestone.Name}'",
                    DaysFromDue = 0
                });
            }
            else if (daysUntilDue <= 3)
            {
                alerts.Add(new MilestoneAlert
                {
                    Milestone = milestone,
                    AlertType = AlertType.DueSoon,
                    Severity = AlertSeverity.Medium,
                    Message = $"Due in {daysUntilDue} day(s): '{milestone.Name}'",
                    DaysFromDue = daysUntilDue
                });
            }
            else if (daysUntilDue <= 7)
            {
                alerts.Add(new MilestoneAlert
                {
                    Milestone = milestone,
                    AlertType = AlertType.Upcoming,
                    Severity = AlertSeverity.Low,
                    Message = $"Upcoming in {daysUntilDue} days: '{milestone.Name}'",
                    DaysFromDue = daysUntilDue
                });
            }
        }

        return alerts.OrderBy(a => a.DaysFromDue).ToList();
    }

    /// <summary>
    /// Gets milestone completion percentage for a project
    /// </summary>
    public decimal GetCompletionPercentage(Guid projectId)
    {
        var projectMilestones = _milestones.Where(m => m.ProjectId == projectId).ToList();
        if (!projectMilestones.Any())
            return 0;

        var completed = projectMilestones.Count(m => m.IsCompleted);
        return (decimal)completed / projectMilestones.Count * 100;
    }

    /// <summary>
    /// Gets critical path milestones
    /// </summary>
    public List<ProjectMilestone> GetCriticalPath(Guid projectId)
    {
        return _milestones
            .Where(m => m.ProjectId == projectId && m.IsCriticalPath && !m.IsCompleted)
            .OrderBy(m => m.DueDate)
            .ToList();
    }

    /// <summary>
    /// Calculates project health based on milestones
    /// </summary>
    public ProjectHealthReport GetProjectHealth(Guid projectId)
    {
        var milestones = _milestones.Where(m => m.ProjectId == projectId).ToList();
        var report = new ProjectHealthReport
        {
            ProjectId = projectId,
            TotalMilestones = milestones.Count,
            CompletedMilestones = milestones.Count(m => m.IsCompleted),
            OverdueMilestones = milestones.Count(m => !m.IsCompleted && m.DueDate < DateTime.Now),
            CompletionPercentage = GetCompletionPercentage(projectId)
        };

        // Calculate health score (0-100)
        var healthScore = 100m;

        // Deduct for overdue milestones
        if (report.OverdueMilestones > 0)
        {
            healthScore -= report.OverdueMilestones * 10;
        }

        // Adjust for completion percentage
        if (report.CompletionPercentage < 25)
        {
            healthScore -= 10;
        }

        // Check critical path
        var criticalOverdue = milestones.Count(m => m.IsCriticalPath && !m.IsCompleted && m.DueDate < DateTime.Now);
        if (criticalOverdue > 0)
        {
            healthScore -= criticalOverdue * 20;
        }

        report.HealthScore = Math.Max(0, Math.Min(100, healthScore));
        report.HealthStatus = GetHealthStatus(report.HealthScore);

        return report;
    }

    private HealthStatus GetHealthStatus(decimal score)
    {
        if (score >= 80) return HealthStatus.Healthy;
        if (score >= 60) return HealthStatus.AtRisk;
        if (score >= 40) return HealthStatus.Critical;
        return HealthStatus.Failed;
    }
}

/// <summary>
/// Project milestone
/// </summary>
public class ProjectMilestone
{
    public Guid MilestoneId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public MilestoneStatus Status { get; set; }
    public MilestonePriority Priority { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsCriticalPath { get; set; }
    public DateTime CreatedDate { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public decimal CompletionPercentage { get; set; }
}

public enum MilestoneStatus
{
    NotStarted,
    InProgress,
    Blocked,
    Completed,
    Cancelled
}

public enum MilestonePriority
{
    Low,
    Medium,
    High,
    Critical
}

public class MilestoneResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public ProjectMilestone? Milestone { get; set; }
    public int DaysUntilDue { get; set; }

    public static MilestoneResult Failure(string message)
        => new() { IsSuccess = false, Message = message };
}

public class MilestoneAlert
{
    public ProjectMilestone Milestone { get; set; } = null!;
    public AlertType AlertType { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DaysFromDue { get; set; }
}

public enum AlertType
{
    Overdue,
    DueToday,
    DueSoon,
    Upcoming
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public class ProjectHealthReport
{
    public Guid ProjectId { get; set; }
    public int TotalMilestones { get; set; }
    public int CompletedMilestones { get; set; }
    public int OverdueMilestones { get; set; }
    public decimal CompletionPercentage { get; set; }
    public decimal HealthScore { get; set; }
    public HealthStatus HealthStatus { get; set; }
}

public enum HealthStatus
{
    Healthy,
    AtRisk,
    Critical,
    Failed
}
