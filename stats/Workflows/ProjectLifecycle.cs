using Gradual.Models;

namespace Gradual.BusinessLogic.Workflows;

/// <summary>
/// Manages project lifecycle state transitions
/// </summary>
public class ProjectLifecycle
{
    private static readonly Dictionary<string, List<string>> _validTransitions = new()
    {
        { "Active", new List<string> { "On Hold", "Completed" } },
        { "On Hold", new List<string> { "Active", "Completed" } },
        { "Completed", new List<string> { "Active" } } // Can reopen
    };

    /// <summary>
    /// Checks if a status transition is valid
    /// </summary>
    public WorkflowResult CanTransition(string currentStatus, string newStatus)
    {
        if (currentStatus == newStatus)
        {
            return WorkflowResult.Success("No status change");
        }

        if (!_validTransitions.ContainsKey(currentStatus))
        {
            return WorkflowResult.Failure($"Invalid current status: {currentStatus}");
        }

        if (!_validTransitions[currentStatus].Contains(newStatus))
        {
            return WorkflowResult.Failure($"Cannot transition from '{currentStatus}' to '{newStatus}'. Valid transitions: {string.Join(", ", _validTransitions[currentStatus])}");
        }

        return WorkflowResult.Success($"Transition from '{currentStatus}' to '{newStatus}' is valid");
    }

    /// <summary>
    /// Gets all valid next statuses for current status
    /// </summary>
    public List<string> GetValidNextStatuses(string currentStatus)
    {
        return _validTransitions.ContainsKey(currentStatus)
            ? _validTransitions[currentStatus]
            : new List<string>();
    }

    /// <summary>
    /// Executes status transition with business logic
    /// </summary>
    public WorkflowTransitionResult TransitionStatus(ProjectRecord project, string newStatus, string reason)
    {
        var canTransition = CanTransition(project.Status, newStatus);
        if (!canTransition.IsSuccess)
        {
            return new WorkflowTransitionResult
            {
                IsSuccess = false,
                Message = canTransition.Message,
                RequiresApproval = false
            };
        }

        // Check if transition requires approval
        var requiresApproval = RequiresApproval(project, newStatus);

        // Check prerequisites
        var prerequisiteCheck = CheckPrerequisites(project, newStatus);
        if (!prerequisiteCheck.IsSuccess)
        {
            return new WorkflowTransitionResult
            {
                IsSuccess = false,
                Message = prerequisiteCheck.Message,
                RequiresApproval = false
            };
        }

        // Execute transition
        var oldStatus = project.Status;
        project.Status = newStatus;
        project.UpdatedAt = DateTime.Now;

        return new WorkflowTransitionResult
        {
            IsSuccess = true,
            Message = $"Status changed from '{oldStatus}' to '{newStatus}'",
            RequiresApproval = requiresApproval,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            TransitionReason = reason
        };
    }

    /// <summary>
    /// Checks if transition requires approval
    /// </summary>
    private bool RequiresApproval(ProjectRecord project, string newStatus)
    {
        // Completing a high-priority project requires approval
        if (newStatus == "Completed" && project.Priority == "High")
        {
            return true;
        }

        // Reopening a completed project requires approval
        if (project.Status == "Completed" && newStatus == "Active")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks prerequisites for status transition
    /// </summary>
    private WorkflowResult CheckPrerequisites(ProjectRecord project, string newStatus)
    {
        switch (newStatus)
        {
            case "Completed":
                if (project.ProjectPhases.Any())
                {
                    // In real system, check if all phases are completed
                    return WorkflowResult.Warning("Ensure all project phases are completed");
                }
                break;

            case "Active":
                if (project.Status == "Completed")
                {
                    return WorkflowResult.Warning("Reopening completed project. Ensure stakeholders are notified.");
                }
                break;

            case "On Hold":
                if (string.IsNullOrWhiteSpace(project.Notes))
                {
                    return WorkflowResult.Failure("Projects put on hold must have notes explaining the reason");
                }
                break;
        }

        return WorkflowResult.Success();
    }
}

/// <summary>
/// Result of workflow validation
/// </summary>
public class WorkflowResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public WorkflowSeverity Severity { get; set; }

    public static WorkflowResult Success(string message = "") 
        => new() { IsSuccess = true, Message = message, Severity = WorkflowSeverity.Info };

    public static WorkflowResult Failure(string message) 
        => new() { IsSuccess = false, Message = message, Severity = WorkflowSeverity.Error };

    public static WorkflowResult Warning(string message) 
        => new() { IsSuccess = true, Message = message, Severity = WorkflowSeverity.Warning };
}

/// <summary>
/// Result of workflow transition
/// </summary>
public class WorkflowTransitionResult : WorkflowResult
{
    public bool RequiresApproval { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string TransitionReason { get; set; } = string.Empty;
    public DateTime TransitionDate { get; set; } = DateTime.Now;
}

public enum WorkflowSeverity
{
    Info,
    Warning,
    Error
}
