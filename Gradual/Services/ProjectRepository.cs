using System.Text.Json;
using Gradual.Interfaces;
using Gradual.Models;

namespace Gradual.Services;

/// <summary>
/// Repository implementation for project data access using JSON file storage
/// Implements async patterns for better responsiveness
/// </summary>
public sealed class ProjectRepository : IProjectRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    public ProjectRepository(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        
        var folder = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }

    public async Task<List<ProjectRecord>> LoadProjectsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                return new List<ProjectRecord>();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<ProjectRecord>();
            }

            var items = JsonSerializer.Deserialize<List<ProjectRecord>>(json, _jsonOptions);
            return items ?? new List<ProjectRecord>();
        }
        catch (Exception ex)
        {
            // Log error (would use logging framework in production)
            throw new IOException($"Failed to load projects from {_filePath}", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ProjectRecord?> GetProjectByIdAsync(Guid id)
    {
        var projects = await LoadProjectsAsync();
        return projects.FirstOrDefault(p => p.Id == id);
    }

    public async Task SaveProjectAsync(ProjectRecord project)
    {
        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        await _lock.WaitAsync();
        try
        {
            var projects = await LoadProjectsInternalAsync();
            var existing = projects.FirstOrDefault(p => p.Id == project.Id);

            if (existing == null)
            {
                projects.Add(project);
            }
            else
            {
                var index = projects.IndexOf(existing);
                projects[index] = project;
            }

            await SaveProjectsInternalAsync(projects);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteProjectAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var projects = await LoadProjectsInternalAsync();
            var remaining = projects.Where(p => p.Id != id).ToList();
            await SaveProjectsInternalAsync(remaining);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ProjectNameExistsAsync(string projectName, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return false;
        }

        var projects = await LoadProjectsAsync();
        return projects.Any(p => 
            p.ProjectName.Equals(projectName, StringComparison.OrdinalIgnoreCase) &&
            (!excludeId.HasValue || p.Id != excludeId.Value));
    }

    public async Task<List<ProjectRecord>> GetProjectsByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return await LoadProjectsAsync();
        }

        var projects = await LoadProjectsAsync();
        return projects
            .Where(p => p.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.UpdatedAt)
            .ToList();
    }

    public async Task<List<ProjectRecord>> GetProjectsByPriorityAsync(string priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return await LoadProjectsAsync();
        }

        var projects = await LoadProjectsAsync();
        return projects
            .Where(p => p.Priority.Equals(priority, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.UpdatedAt)
            .ToList();
    }

    public async Task<List<ProjectRecord>> SearchProjectsAsync(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return await LoadProjectsAsync();
        }

        var projects = await LoadProjectsAsync();
        var search = searchText.ToLowerInvariant();

        return projects
            .Where(p =>
                p.ProjectName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Client.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Status.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Priority.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.FolderPath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Notes.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.ProjectPhases.Any(phase => phase.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                p.AttachmentPaths.Any(path => path.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => p.UpdatedAt)
            .ToList();
    }

    // Private helper methods (not thread-safe, must be called within lock)
    private async Task<List<ProjectRecord>> LoadProjectsInternalAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new List<ProjectRecord>();
        }

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<ProjectRecord>();
        }

        var items = JsonSerializer.Deserialize<List<ProjectRecord>>(json, _jsonOptions);
        return items ?? new List<ProjectRecord>();
    }

    private async Task SaveProjectsInternalAsync(IEnumerable<ProjectRecord> projects)
    {
        var json = JsonSerializer.Serialize(projects.ToList(), _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    /// <summary>
    /// Creates a timestamped backup of the projects data file.
    /// Old backups beyond <paramref name="maxBackupFiles"/> are pruned automatically.
    /// </summary>
    public async Task CreateBackupAsync(string backupDirectory, int maxBackupFiles = 10)
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            Directory.CreateDirectory(backupDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"projects_backup_{timestamp}.json";
            var backupPath = Path.Combine(backupDirectory, backupFileName);

            await _lock.WaitAsync();
            try
            {
                File.Copy(_filePath, backupPath, overwrite: false);
            }
            finally
            {
                _lock.Release();
            }

            // Prune old backups — keep only the most recent maxBackupFiles
            var existingBackups = Directory.GetFiles(backupDirectory, "projects_backup_*.json")
                .OrderByDescending(f => f)
                .Skip(maxBackupFiles)
                .ToList();

            foreach (var old in existingBackups)
            {
                try { File.Delete(old); } catch { /* best-effort */ }
            }
        }
        catch (Exception ex)
        {
            throw new IOException($"Backup failed for {_filePath}", ex);
        }
    }
}
