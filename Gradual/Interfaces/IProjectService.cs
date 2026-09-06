using Gradual.Models;
using Gradual.Models.DTOs;

namespace Gradual.Interfaces;

/// <summary>
/// Service interface for all project business-logic operations.
/// Groups are numbered to match the 100-feature implementation plan.
/// </summary>
public interface IProjectService
{
    // ── GROUP A / Core CRUD ───────────────────────────────────────────────────
    Task<ServiceResult<List<ProjectRecord>>> GetAllProjectsAsync();
    Task<ServiceResult<ProjectRecord>> GetProjectByIdAsync(Guid id);
    Task<ServiceResult<ProjectRecord>> CreateProjectAsync(ProjectDto projectDto);
    Task<ServiceResult<ProjectRecord>> UpdateProjectAsync(Guid id, ProjectDto projectDto);
    Task<ServiceResult<bool>> DeleteProjectAsync(Guid id);

    // ── GROUP A / Filtering ───────────────────────────────────────────────────
    Task<ServiceResult<List<ProjectRecord>>> GetProjectsByStatusAsync(string status);
    Task<ServiceResult<List<ProjectRecord>>> GetProjectsByPriorityAsync(string priority);
    Task<ServiceResult<List<ProjectRecord>>> SearchProjectsAsync(string searchText);

    // ── GROUP A / Export (original) ───────────────────────────────────────────
    Task<ServiceResult<bool>> ExportToExcelAsync(IEnumerable<ProjectRecord> projects, string filePath);

    // ── GROUP B — CRUD & Lifecycle ────────────────────────────────────────────
    /// <summary>Feature 11 — Archive a project (sets Status="Archived").</summary>
    Task<ServiceResult<ProjectRecord>> ArchiveProjectAsync(Guid id);

    /// <summary>Feature 12 — Deep-clone a project; new name gets " (copy)" suffix.</summary>
    Task<ServiceResult<ProjectRecord>> CloneProjectAsync(Guid sourceId, string? newName = null);

    /// <summary>Feature 13 — Bulk status change on a set of project IDs.</summary>
    Task<ServiceResult<int>> BulkUpdateStatusAsync(IEnumerable<Guid> ids, string newStatus);

    /// <summary>Feature 14 — Bulk delete a set of project IDs.</summary>
    Task<ServiceResult<int>> BulkDeleteAsync(IEnumerable<Guid> ids);

    /// <summary>Feature 16/17 — Soft-delete (sets DeletedAt). Returns all soft-deleted projects.</summary>
    Task<ServiceResult<List<ProjectRecord>>> GetDeletedProjectsAsync();

    /// <summary>Feature 17 — Restore a soft-deleted project (clears DeletedAt).</summary>
    Task<ServiceResult<ProjectRecord>> RestoreProjectAsync(Guid id);

    /// <summary>Feature 18 — Permanently purge all soft-deleted projects.</summary>
    Task<ServiceResult<int>> PurgeDeletedProjectsAsync();

    // ── GROUP B Continued ─────────────────────────────────────────────────────
    /// <summary>Feature 9 — Toggle IsStarred.</summary>
    Task<ServiceResult<ProjectRecord>> ToggleStarAsync(Guid id);

    /// <summary>Feature 3 — Update only the completion percent.</summary>
    Task<ServiceResult<ProjectRecord>> SetCompletionAsync(Guid id, int percent);

    /// <summary>Feature 8 — Create a new project from an existing template project.</summary>
    Task<ServiceResult<ProjectRecord>> CreateFromTemplateAsync(Guid templateId, string newName);

    // ── GROUP C — Advanced Filtering ─────────────────────────────────────────
    /// <summary>Feature 22 — Filter by created/due date range.</summary>
    Task<ServiceResult<List<ProjectRecord>>> GetProjectsByDateRangeAsync(DateTime? from, DateTime? to, bool useDueDate = false);

    /// <summary>Feature 23 — Filter by one or more tags (AND mode = all tags must match).</summary>
    Task<ServiceResult<List<ProjectRecord>>> GetProjectsByTagsAsync(IEnumerable<string> tags, bool andMode = false);

    /// <summary>Feature 24 — Filter by client name.</summary>
    Task<ServiceResult<List<ProjectRecord>>> GetProjectsByClientAsync(string clientName);

    /// <summary>Feature 29 — Get projects matching a named preset (Active+High, Overdue, etc.).</summary>
    Task<ServiceResult<List<ProjectRecord>>> GetProjectsByPresetAsync(string presetName);

    /// <summary>Feature 52 — Get all currently overdue projects.</summary>
    Task<ServiceResult<List<ProjectRecord>>> GetOverdueProjectsAsync();

    /// <summary>Feature 59 — Get projects due today.</summary>
    Task<ServiceResult<List<ProjectRecord>>> GetDueTodayProjectsAsync();

    // ── GROUP C / Statistics ──────────────────────────────────────────────────
    Task<ServiceResult<List<string>>> GetUniqueClientsAsync();
    Task<ServiceResult<List<string>>> GetUniqueTagsAsync();
    Task<ServiceResult<Dictionary<string, int>>> GetProjectCountByStatusAsync();
    Task<ServiceResult<Dictionary<string, int>>> GetProjectCountByClientAsync();

    // ── GROUP D — Import / Export ─────────────────────────────────────────────
    /// <summary>Feature 31 — Export to CSV.</summary>
    Task<ServiceResult<bool>> ExportToCsvAsync(IEnumerable<ProjectRecord> projects, string filePath, char separator = ',');

    /// <summary>Feature 32 — Export to pretty-printed JSON.</summary>
    Task<ServiceResult<string>> ExportToJsonAsync(IEnumerable<ProjectRecord> projects);

    /// <summary>Feature 34 — Import from CSV; returns count of imported rows.</summary>
    Task<ServiceResult<int>> ImportFromCsvAsync(string filePath, bool skipDuplicates = true);

    /// <summary>Feature 35 — Import from JSON string; returns count of imported records.</summary>
    Task<ServiceResult<int>> ImportFromJsonAsync(string jsonData, bool overwriteExisting = false);

    /// <summary>Feature 36 — Import from Excel file (.xlsx); returns count of imported rows.</summary>
    Task<ServiceResult<int>> ImportFromExcelAsync(string filePath, bool skipDuplicates = true);

    // ── GROUP F / System ──────────────────────────────────────────────────────
    /// <summary>Feature 91 — Create timestamped backup ZIP of the data directory.</summary>
    Task<ServiceResult<string>> CreateBackupAsync(string? backupDirectory = null);

    /// <summary>Feature 92 — Restore from a backup ZIP.</summary>
    Task<ServiceResult<bool>> RestoreFromBackupAsync(string backupPath);

    /// <summary>Feature 93 — Run data integrity checks; returns list of issues found.</summary>
    Task<ServiceResult<List<string>>> CheckDataIntegrityAsync();

    // ── GROUP J / Recurrence ──────────────────────────────────────────────────
    /// <summary>Feature 10 — Process all projects with recurrence rules; clone if due date passed.</summary>
    Task<ServiceResult<int>> ProcessRecurringProjectsAsync();
}

