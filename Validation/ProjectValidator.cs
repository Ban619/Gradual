using Gradual.Models.DTOs;

namespace Gradual.Validation;

/// <summary>
/// Validates project data before operations.
/// Rules are aligned with unit-test expectations and appsettings.json limits.
/// </summary>
public class ProjectValidator : IValidator<ProjectDto>
{
    // Mutable so Setup → Validation can push updated values live without restart.
    private int _maxProjectNameLength = 200;
    private int _maxClientNameLength  = 200;
    private int _maxNotesLength       = 5000;
    private int _maxFolderPathLength  = 500;
    private int _maxPhases            = 50;
    private int _maxPhaseLength       = 100;
    private int _maxAttachments       = 100;

    private readonly string[] _validStatuses    = { "Active", "On Hold", "Completed" };
    private readonly string[] _validPriorities  = { "High", "Normal", "Low" };

    /// <summary>
    /// Called by the Setup panel after the user saves new validation limits
    /// so they take effect immediately without requiring an app restart.
    /// </summary>
    public void UpdateLimits(
        int maxProjectName, int maxClient, int maxNotes,
        int maxFolder,      int maxPhases, int maxPhaseLen,
        int maxAttachments)
    {
        _maxProjectNameLength = Math.Max(1, maxProjectName);
        _maxClientNameLength  = Math.Max(1, maxClient);
        _maxNotesLength       = Math.Max(1, maxNotes);
        _maxFolderPathLength  = Math.Max(1, maxFolder);
        _maxPhases            = Math.Max(1, maxPhases);
        _maxPhaseLength       = Math.Max(1, maxPhaseLen);
        _maxAttachments       = Math.Max(1, maxAttachments);
    }

    public ValidationResult Validate(ProjectDto project)
    {
        var result = new ValidationResult { IsValid = true };

        // ── Project Name ──────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(project.ProjectName))
        {
            result.AddError("Project name is required.");
        }
        else if (project.ProjectName.Length > _maxProjectNameLength)
        {
            result.AddError($"Project name cannot exceed {_maxProjectNameLength} characters.");
        }

        // ── Client (required) ─────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(project.Client))
        {
            result.AddError("Client name is required.");
        }
        else if (project.Client.Length > _maxClientNameLength)
        {
            result.AddError($"Client name cannot exceed {_maxClientNameLength} characters.");
        }

        // ── Status ────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(project.Status))
        {
            result.AddError("Status is required.");
        }
        else if (!_validStatuses.Contains(project.Status, StringComparer.OrdinalIgnoreCase))
        {
            result.AddError($"Status must be one of: {string.Join(", ", _validStatuses)}");
        }

        // ── Priority ──────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(project.Priority))
        {
            result.AddError("Priority is required.");
        }
        else if (!_validPriorities.Contains(project.Priority, StringComparer.OrdinalIgnoreCase))
        {
            result.AddError($"Priority must be one of: {string.Join(", ", _validPriorities)}");
        }

        // ── Notes ─────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(project.Notes) && project.Notes.Length > _maxNotesLength)
        {
            result.AddError($"Notes cannot exceed {_maxNotesLength} characters.");
        }

        // ── Folder Path ───────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(project.FolderPath))
        {
            if (project.FolderPath.Length > _maxFolderPathLength)
            {
                result.AddError($"Folder path cannot exceed {_maxFolderPathLength} characters.");
            }

            if (project.FolderPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                result.AddError("Folder path contains invalid characters.");
            }
        }

        // ── Attachment Paths ──────────────────────────────────────────────
        if (project.AttachmentPaths != null)
        {
            if (project.AttachmentPaths.Count > _maxAttachments)
            {
                result.AddError($"Cannot exceed {_maxAttachments} attachments per project.");
            }
            else
            {
                foreach (var path in project.AttachmentPaths)
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        result.AddError("Attachment paths cannot be empty.");
                    }
                    else if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    {
                        result.AddError($"Attachment path '{path}' contains invalid characters.");
                    }
                }
            }
        }

        // ── Project Phases ────────────────────────────────────────────────
        if (project.ProjectPhases != null)
        {
            if (project.ProjectPhases.Count > _maxPhases)
            {
                result.AddError($"Cannot exceed {_maxPhases} phases per project.");
            }
            else
            {
                foreach (var phase in project.ProjectPhases)
                {
                    if (string.IsNullOrWhiteSpace(phase))
                    {
                        result.AddError("Project phases cannot contain empty values.");
                        break;
                    }
                    else if (phase.Length > _maxPhaseLength)
                    {
                        result.AddError($"Each phase name cannot exceed {_maxPhaseLength} characters.");
                        break;
                    }
                }
            }
        }

        return result;
    }
}
