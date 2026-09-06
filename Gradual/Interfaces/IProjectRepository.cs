using Gradual.Models;

namespace Gradual.Interfaces;

/// <summary>
/// Repository interface for data access operations
/// </summary>
public interface IProjectRepository
{
    /// <summary>
    /// Loads all projects from the data store
    /// </summary>
    Task<List<ProjectRecord>> LoadProjectsAsync();

    /// <summary>
    /// Gets a project by its ID
    /// </summary>
    Task<ProjectRecord?> GetProjectByIdAsync(Guid id);

    /// <summary>
    /// Saves a new or existing project
    /// </summary>
    Task SaveProjectAsync(ProjectRecord project);

    /// <summary>
    /// Deletes a project by its ID
    /// </summary>
    Task DeleteProjectAsync(Guid id);

    /// <summary>
    /// Checks if a project with the given name already exists (excluding specific ID)
    /// </summary>
    Task<bool> ProjectNameExistsAsync(string projectName, Guid? excludeId = null);

    /// <summary>
    /// Gets projects filtered by status
    /// </summary>
    Task<List<ProjectRecord>> GetProjectsByStatusAsync(string status);

    /// <summary>
    /// Gets projects filtered by priority
    /// </summary>
    Task<List<ProjectRecord>> GetProjectsByPriorityAsync(string priority);

    /// <summary>
    /// Searches projects by text across multiple fields
    /// </summary>
    Task<List<ProjectRecord>> SearchProjectsAsync(string searchText);
}

// ============================================================================
// FUTURE ENHANCEMENTS - Methods to implement later
// ============================================================================
/*
 * Add these methods when you're ready to enhance the repository:
 * 
 * // Pagination
 * Task<PagedResult<ProjectRecord>> GetProjectsPagedAsync(int pageNumber, int pageSize);
 * 
 * // Advanced queries
 * Task<List<ProjectRecord>> GetProjectsByClientAsync(string clientName);
 * Task<List<ProjectRecord>> GetProjectsByDateRangeAsync(DateTime startDate, DateTime endDate);
 * Task<List<ProjectRecord>> GetRecentlyUpdatedProjectsAsync(int count = 10);
 * 
 * // Batch operations
 * Task<int> DeleteProjectsAsync(IEnumerable<Guid> ids);
 * Task SaveProjectsBatchAsync(IEnumerable<ProjectRecord> projects);
 * 
 * // Statistics
 * Task<int> GetTotalProjectCountAsync();
 * Task<Dictionary<string, int>> GetProjectCountByStatusAsync();
 * Task<Dictionary<string, int>> GetProjectCountByClientAsync();
 * Task<List<string>> GetUniqueClientsAsync();
 * 
 * // Backup & Export
 * Task<string> CreateBackupAsync(string? backupPath = null);
 * Task<bool> RestoreFromBackupAsync(string backupPath);
 * Task<string> ExportToJsonAsync(IEnumerable<Guid>? projectIds = null);
 * Task<int> ImportFromJsonAsync(string jsonData, bool overwriteExisting = false);
 * 
 * See INTERFACES_ENHANCEMENT_SUMMARY.md for full list of 45+ enhancement methods
 */
