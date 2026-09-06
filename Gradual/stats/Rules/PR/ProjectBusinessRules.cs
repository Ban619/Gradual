using Gradual.Models;

namespace Gradual.BusinessLogic.Rules.ProjectRules;

/// <summary>
/// Project cannot be completed if it has open phases
/// </summary>
public class ProjectCompletionRule : IBusinessRule<ProjectRecord>
{
    public string RuleName => "ProjectCompletion";
    public string Description => "Project cannot be marked as completed if it has incomplete phases";
    public RuleSeverity Severity => RuleSeverity.Error;

    public RuleResult Evaluate(ProjectRecord project)
    {
        if (project.Status == "Completed" && project.ProjectPhases.Any())
        {
            // In a real system, you'd check phase completion status
            // For now, we'll just warn if there are phases
            return RuleResult.Warning($"Project '{project.ProjectName}' is being completed but has {project.ProjectPhases.Count} phases. Ensure all phases are complete.");
        }

        return RuleResult.Success();
    }
}

/// <summary>
/// High priority projects must have a client assigned
/// </summary>
public class HighPriorityClientRule : IBusinessRule<ProjectRecord>
{
    public string RuleName => "HighPriorityClient";
    public string Description => "High priority projects must have a client assigned";
    public RuleSeverity Severity => RuleSeverity.Error;

    public RuleResult Evaluate(ProjectRecord project)
    {
        if (project.Priority == "High" && string.IsNullOrWhiteSpace(project.Client))
        {
            return RuleResult.Failure("High priority projects must have a client assigned");
        }

        return RuleResult.Success();
    }
}

/// <summary>
/// Projects must have a folder path if status is Active
/// </summary>
public class ActiveProjectFolderRule : IBusinessRule<ProjectRecord>
{
    public string RuleName => "ActiveProjectFolder";
    public string Description => "Active projects should have a working folder path";
    public RuleSeverity Severity => RuleSeverity.Warning;

    public RuleResult Evaluate(ProjectRecord project)
    {
        if (project.Status == "Active" && string.IsNullOrWhiteSpace(project.FolderPath))
        {
            return RuleResult.Warning($"Active project '{project.ProjectName}' should have a working folder path assigned");
        }

        return RuleResult.Success();
    }
}

/// <summary>
/// Projects cannot be deleted if they have attachments
/// </summary>
public class ProjectDeletionRule : IBusinessRule<ProjectRecord>
{
    public string RuleName => "ProjectDeletion";
    public string Description => "Projects with attachments should be archived rather than deleted";
    public RuleSeverity Severity => RuleSeverity.Warning;

    public RuleResult Evaluate(ProjectRecord project)
    {
        if (project.AttachmentPaths.Any())
        {
            return RuleResult.Warning($"Project '{project.ProjectName}' has {project.AttachmentPaths.Count} attachments. Consider archiving instead of deletion.");
        }

        return RuleResult.Success();
    }
}

/// <summary>
/// Project name must be unique within the same client
/// </summary>
public class UniqueProjectNamePerClientRule : IBusinessRule<(ProjectRecord Project, List<ProjectRecord> AllProjects)>
{
    public string RuleName => "UniqueProjectNamePerClient";
    public string Description => "Project names must be unique within the same client";
    public RuleSeverity Severity => RuleSeverity.Error;

    public RuleResult Evaluate((ProjectRecord Project, List<ProjectRecord> AllProjects) context)
    {
        var (project, allProjects) = context;
        
        var duplicate = allProjects.FirstOrDefault(p => 
            p.Id != project.Id &&
            p.Client.Equals(project.Client, StringComparison.OrdinalIgnoreCase) &&
            p.ProjectName.Equals(project.ProjectName, StringComparison.OrdinalIgnoreCase));

        if (duplicate != null)
        {
            return RuleResult.Failure($"Client '{project.Client}' already has a project named '{project.ProjectName}'");
        }

        return RuleResult.Success();
    }
}

/// <summary>
/// Projects on hold should have notes explaining why
/// </summary>
public class OnHoldReasonRule : IBusinessRule<ProjectRecord>
{
    public string RuleName => "OnHoldReason";
    public string Description => "Projects on hold should have notes explaining the reason";
    public RuleSeverity Severity => RuleSeverity.Warning;

    public RuleResult Evaluate(ProjectRecord project)
    {
        if (project.Status == "On Hold" && string.IsNullOrWhiteSpace(project.Notes))
        {
            return RuleResult.Warning($"Project '{project.ProjectName}' is on hold but has no notes explaining why");
        }

        return RuleResult.Success();
    }
}

/// <summary>
/// Project phases should not exceed recommended limit
/// </summary>
public class ProjectPhaseCountRule : IBusinessRule<ProjectRecord>
{
    private const int RecommendedMaxPhases = 10;
    
    public string RuleName => "ProjectPhaseCount";
    public string Description => "Projects should not have excessive phases";
    public RuleSeverity Severity => RuleSeverity.Warning;

    public RuleResult Evaluate(ProjectRecord project)
    {
        if (project.ProjectPhases.Count > RecommendedMaxPhases)
        {
            return RuleResult.Warning($"Project '{project.ProjectName}' has {project.ProjectPhases.Count} phases. Consider breaking into sub-projects (recommended max: {RecommendedMaxPhases})");
        }

        return RuleResult.Success();
    }
}
