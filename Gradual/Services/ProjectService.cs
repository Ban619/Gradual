using Gradual.Infrastructure;
using System.IO.Compression;
using Gradual.Exceptions;
using Gradual.Interfaces;
using Gradual.Models;
using Gradual.Models.DTOs;
using Gradual.Validation;
using Gradual.BusinessLogic.Rules;
using Gradual.BusinessLogic.Rules.ProjectRules;
using Gradual.BusinessLogic.Workflows;
using Gradual.BusinessLogic.Audit;
using Gradual.BusinessLogic.Notifications;

namespace Gradual.Services;

/// <summary>
/// Business logic layer for project operations
/// Handles validation, business rules, and coordinates between UI and repository
/// </summary>
public class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;
    private readonly IValidator<ProjectDto> _validator;
    private readonly LoggingService _loggingService;
    private readonly AnalyticsService _analyticsService;
    private readonly WebhookService _webhookService;
    private readonly ErrorTrackingService _errorTrackingService;
    private readonly BusinessRuleEngine<ProjectDto> _ruleEngine;
    private readonly ProjectLifecycle _workflowManager;
    private readonly AuditTrailManager _auditManager;
    private readonly Notif _notif;

    public ProjectService(
        IProjectRepository repository, 
        IValidator<ProjectDto> validator,
        LoggingService loggingService,
        AnalyticsService analyticsService,
        WebhookService webhookService,
        ErrorTrackingService errorTrackingService,
        BusinessRuleEngine<ProjectDto> ruleEngine,
        ProjectLifecycle workflowManager,
        AuditTrailManager auditManager,
        Notif notif)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _webhookService = webhookService ?? throw new ArgumentNullException(nameof(webhookService));
        _errorTrackingService = errorTrackingService ?? throw new ArgumentNullException(nameof(errorTrackingService));
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _workflowManager = workflowManager ?? throw new ArgumentNullException(nameof(workflowManager));
        _auditManager = auditManager ?? throw new ArgumentNullException(nameof(auditManager));
        _notif = notif ?? throw new ArgumentNullException(nameof(notif));
    }

    public async Task<ServiceResult<List<ProjectRecord>>> GetAllProjectsAsync()
    {
        try
        {
            _loggingService.LogDebug("Loading all projects");
            var startTime = DateTime.Now;
            
            var projects = await _repository.LoadProjectsAsync();
            // Filter out soft-deleted projects for normal view (Feature 16)
            var visible = projects.Where(p => !p.IsDeleted).ToList();
            
            var duration = DateTime.Now - startTime;
            _analyticsService.TrackPerformance("LoadAllProjects", duration);
            _loggingService.LogInformation("Loaded {Count} projects in {Duration}ms", visible.Count, duration.TotalMilliseconds);
            
            return ServiceResult<List<ProjectRecord>>.SuccessResult(visible);
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to load projects");
            await _errorTrackingService.TrackErrorAsync(ex, "GetAllProjectsAsync");
            
            return ServiceResult<List<ProjectRecord>>.FailureResult(
                "Failed to load projects", 
                ex.Message);
        }
    }

    public async Task<ServiceResult<ProjectRecord>> GetProjectByIdAsync(Guid id)
    {
        try
        {
            _loggingService.LogDebug("Getting project by ID: {ProjectId}", id);
            
            var project = await _repository.GetProjectByIdAsync(id);
            
            if (project == null)
            {
                _loggingService.LogWarning("Project not found: {ProjectId}", id);
                return ServiceResult<ProjectRecord>.FailureResult(
                    $"Project with ID {id} not found");
            }

            return ServiceResult<ProjectRecord>.SuccessResult(project);
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to retrieve project {ProjectId}", id);
            await _errorTrackingService.TrackErrorAsync(ex, "GetProjectByIdAsync");
            
            return ServiceResult<ProjectRecord>.FailureResult(
                "Failed to retrieve project", 
                ex.Message);
        }
    }

    public async Task<ServiceResult<ProjectRecord>> CreateProjectAsync(ProjectDto projectDto)
    {
        try
        {
            _loggingService.LogInformation("Creating new project: {ProjectName}", projectDto.ProjectName);
            
            // Validate input
            var validationResult = _validator.Validate(projectDto);
            if (!validationResult.IsValid)
            {
                _loggingService.LogWarning("Validation failed for project: {ProjectName}", projectDto.ProjectName);
                return ServiceResult<ProjectRecord>.FailureResult(
                    "Validation failed", 
                    validationResult.Errors);
            }

            // Evaluate business rules
            var ruleResult = _ruleEngine.Evaluate(projectDto);
            if (ruleResult.HasCriticalFailures)
            {
                _loggingService.LogWarning("Business rules violated for project: {ProjectName}", projectDto.ProjectName);
                var errors = ruleResult.RuleResults
                    .Where(r => r.Severity == RuleSeverity.Critical && !r.Result.IsValid)
                    .Select(r => r.Result.Message)
                    .ToList();
                return ServiceResult<ProjectRecord>.FailureResult(
                    "Business rule violation", 
                    errors);
            }

            // Log warnings if any
            if (ruleResult.HasWarnings)
            {
                var warnings = ruleResult.RuleResults
                    .Where(r => r.Severity == RuleSeverity.Warning)
                    .ToList();
                foreach (var warning in warnings)
                {
                    _loggingService.LogWarning("Business rule warning: {Message}", warning.Result.Message);
                }
            }

            // Map DTO to entity
            var project = new ProjectRecord
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            ApplyDtoToEntity(project, projectDto);

            // Save to repository
            await _repository.SaveProjectAsync(project);
            
            // Record audit
            _auditManager.RecordAudit(
                entityType: "Project",
                entityId: project.Id,
                action: BusinessLogic.Audit.AuditAction.Create,
                performedBy: Environment.UserName,
                newValues: new Dictionary<string, object>
                {
                    ["ProjectName"] = project.ProjectName,
                    ["Status"] = project.Status,
                    ["Priority"] = project.Priority,
                    ["Client"] = project.Client
                },
                notes: "Project created"
            );
            
            // Track analytics and send webhook
            _analyticsService.TrackProjectCreated(project.ProjectName, project.Status, project.Priority);
            await _webhookService.NotifyProjectCreatedAsync(project);
            
            _loggingService.LogInformation("Project created successfully: {ProjectName} (ID: {ProjectId})", 
                project.ProjectName, project.Id);

            return ServiceResult<ProjectRecord>.SuccessResult(
                project, 
                "Project created successfully");
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to create project: {ProjectName}", projectDto.ProjectName);
            await _errorTrackingService.TrackErrorAsync(ex, "CreateProjectAsync");
            
            return ServiceResult<ProjectRecord>.FailureResult(
                "Failed to create project", 
                ex.Message);
        }
    }

    public async Task<ServiceResult<ProjectRecord>> UpdateProjectAsync(Guid id, ProjectDto projectDto)
    {
        try
        {
            _loggingService.LogInformation("Updating project: {ProjectId}", id);
            
            // Validate input
            var validationResult = _validator.Validate(projectDto);
            if (!validationResult.IsValid)
            {
                _loggingService.LogWarning("Validation failed for project update: {ProjectId}", id);
                return ServiceResult<ProjectRecord>.FailureResult(
                    "Validation failed", 
                    validationResult.Errors);
            }

            // Check if project exists
            var existingProject = await _repository.GetProjectByIdAsync(id);
            if (existingProject == null)
            {
                _loggingService.LogWarning("Project not found for update: {ProjectId}", id);
                return ServiceResult<ProjectRecord>.FailureResult(
                    $"Project with ID {id} not found");
            }

            // Evaluate business rules
            var ruleResult = _ruleEngine.Evaluate(projectDto);
            if (ruleResult.HasCriticalFailures)
            {
                _loggingService.LogWarning("Business rules violated for project update: {ProjectId}", id);
                var errors = ruleResult.RuleResults
                    .Where(r => r.Severity == RuleSeverity.Critical && !r.Result.IsValid)
                    .Select(r => r.Result.Message)
                    .ToList();
                return ServiceResult<ProjectRecord>.FailureResult(
                    "Business rule violation", 
                    errors);
            }

            // Capture old values for audit
            var oldValues = new Dictionary<string, object>
            {
                ["ProjectName"] = existingProject.ProjectName,
                ["Status"] = existingProject.Status,
                ["Priority"] = existingProject.Priority,
                ["Client"] = existingProject.Client,
                ["FolderPath"] = existingProject.FolderPath,
                ["Notes"] = existingProject.Notes
            };

            // Check if status is changing and validate workflow (Feature 19 — status history)
            if (existingProject.Status != projectDto.Status && projectDto.Status != null)
            {
                var transitionResult = _workflowManager.TransitionStatus(
                    project: existingProject,
                    newStatus: projectDto.Status ?? existingProject.Status,
                    reason: projectDto.StatusChangeReason.IfEmpty("Status updated via UI")
                );

                if (!transitionResult.IsSuccess)
                {
                    _loggingService.LogWarning("Invalid status transition: {Message}", transitionResult.Message);
                    return ServiceResult<ProjectRecord>.FailureResult(transitionResult.Message);
                }

                // Record in status history
                existingProject.StatusHistory.Add(new StatusChange
                {
                    FromStatus = existingProject.Status,
                    ToStatus = projectDto.Status,
                    Reason = projectDto.StatusChangeReason.IfEmpty("Updated via UI")
                });
            }

            // Update entity with DTO values
            ApplyDtoToEntity(existingProject, projectDto);
            existingProject.UpdatedAt = DateTime.Now;

            // Capture new values for audit
            var newValues = new Dictionary<string, object>
            {
                ["ProjectName"] = existingProject.ProjectName,
                ["Status"] = existingProject.Status,
                ["Priority"] = existingProject.Priority,
                ["Client"] = existingProject.Client,
                ["FolderPath"] = existingProject.FolderPath,
                ["Notes"] = existingProject.Notes
            };

            // Save changes
            await _repository.SaveProjectAsync(existingProject);
            
            // Record audit
            _auditManager.RecordAudit(
                entityType: "Project",
                entityId: id,
                action: BusinessLogic.Audit.AuditAction.Update,
                performedBy: Environment.UserName,
                oldValues: oldValues,
                newValues: newValues,
                notes: "Project updated"
            );
            
            // Track analytics and send webhook
            _analyticsService.TrackProjectUpdated(existingProject.ProjectName, "Full Update");
            await _webhookService.NotifyProjectUpdatedAsync(existingProject);
            
            _loggingService.LogInformation("Project updated successfully: {ProjectName} (ID: {ProjectId})", 
                existingProject.ProjectName, existingProject.Id);

            return ServiceResult<ProjectRecord>.SuccessResult(
                existingProject, 
                "Project updated successfully");
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to update project: {ProjectId}", id);
            await _errorTrackingService.TrackErrorAsync(ex, "UpdateProjectAsync");
            
            return ServiceResult<ProjectRecord>.FailureResult(
                "Failed to update project", 
                ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> DeleteProjectAsync(Guid id)
    {
        try
        {
            _loggingService.LogInformation("Deleting project: {ProjectId}", id);
            
            // Check if project exists
            var project = await _repository.GetProjectByIdAsync(id);
            if (project == null)
            {
                _loggingService.LogWarning("Project not found for deletion: {ProjectId}", id);
                return ServiceResult<bool>.FailureResult(
                    $"Project with ID {id} not found");
            }

            var projectName = project.ProjectName;
            
            // Capture values for audit
            var oldValues = new Dictionary<string, object>
            {
                ["ProjectName"] = project.ProjectName,
                ["Status"] = project.Status,
                ["Priority"] = project.Priority,
                ["Client"] = project.Client
            };
            
            await _repository.DeleteProjectAsync(id);
            
            // Record audit
            _auditManager.RecordAudit(
                entityType: "Project",
                entityId: id,
                action: BusinessLogic.Audit.AuditAction.Delete,
                performedBy: Environment.UserName,
                oldValues: oldValues,
                notes: $"Project '{projectName}' deleted"
            );
            
            // Track analytics and send webhook
            _analyticsService.TrackProjectDeleted(projectName);
            await _webhookService.NotifyProjectDeletedAsync(id, projectName);
            
            _loggingService.LogInformation("Project deleted successfully: {ProjectName} (ID: {ProjectId})", 
                projectName, id);

            return ServiceResult<bool>.SuccessResult(
                true, 
                "Project deleted successfully");
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to delete project: {ProjectId}", id);
            await _errorTrackingService.TrackErrorAsync(ex, "DeleteProjectAsync");
            
            return ServiceResult<bool>.FailureResult(
                "Failed to delete project", 
                ex.Message);
        }
    }

    public async Task<ServiceResult<List<ProjectRecord>>> GetProjectsByStatusAsync(string status)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return await GetAllProjectsAsync();
            }

            var projects = await _repository.GetProjectsByStatusAsync(status);
            return ServiceResult<List<ProjectRecord>>.SuccessResult(projects);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<ProjectRecord>>.FailureResult(
                "Failed to filter projects by status", 
                ex.Message);
        }
    }

    public async Task<ServiceResult<List<ProjectRecord>>> GetProjectsByPriorityAsync(string priority)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(priority))
            {
                return await GetAllProjectsAsync();
            }

            var projects = await _repository.GetProjectsByPriorityAsync(priority);
            return ServiceResult<List<ProjectRecord>>.SuccessResult(projects);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<ProjectRecord>>.FailureResult(
                "Failed to filter projects by priority", 
                ex.Message);
        }
    }

    public async Task<ServiceResult<List<ProjectRecord>>> SearchProjectsAsync(string searchText)
    {
        try
        {
            _loggingService.LogDebug("Searching projects: {SearchText}", searchText);
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return await GetAllProjectsAsync();
            }

            var startTime = DateTime.Now;
            var projects = await _repository.SearchProjectsAsync(searchText);
            var duration = DateTime.Now - startTime;
            
            _analyticsService.TrackSearch(searchText, projects.Count);
            _analyticsService.TrackPerformance("SearchProjects", duration);
            
            return ServiceResult<List<ProjectRecord>>.SuccessResult(projects);
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to search projects");
            await _errorTrackingService.TrackErrorAsync(ex, "SearchProjectsAsync");
            
            return ServiceResult<List<ProjectRecord>>.FailureResult(
                "Failed to search projects", 
                ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> ExportToExcelAsync(IEnumerable<ProjectRecord> projects, string filePath)
    {
        try
        {
            _loggingService.LogInformation("Exporting projects to Excel: {FilePath}", filePath);
            
            if (projects == null || !projects.Any())
            {
                return ServiceResult<bool>.FailureResult(
                    "No projects to export");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ServiceResult<bool>.FailureResult(
                    "File path is required");
            }

            var startTime = DateTime.Now;
            
            // Perform export (using existing ProjectExporter)
            await Task.Run(() => ProjectExporter.ExportToExcel(projects, filePath));
            
            var duration = DateTime.Now - startTime;
            var count = projects.Count();
            
            _analyticsService.TrackExport("Excel", count);
            _analyticsService.TrackPerformance("ExportToExcel", duration);
            _loggingService.LogInformation("Exported {Count} projects in {Duration}ms", count, duration.TotalMilliseconds);

            return ServiceResult<bool>.SuccessResult(
                true, 
                $"Exported {count} projects successfully");
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Failed to export projects");
            await _errorTrackingService.TrackErrorAsync(ex, "ExportToExcelAsync");
            
            return ServiceResult<bool>.FailureResult(
                "Failed to export projects", 
                ex.Message);
        }
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies all ProjectDto fields onto the target ProjectRecord entity.
    /// Used for both create (new entity) and update (existing entity) paths,
    /// keeping both in sync automatically when new fields are added.
    /// </summary>
    private static void ApplyDtoToEntity(ProjectRecord entity, ProjectDto dto)
    {
        // ── Original fields ────────────────────────────────────────────────
        entity.ProjectName = dto.ProjectName?.Trim() ?? string.Empty;
        entity.Client = dto.Client?.Trim() ?? string.Empty;
        entity.Status = dto.Status?.Trim() ?? "Active";
        entity.Priority = dto.Priority?.Trim() ?? "Normal";
        entity.FolderPath = dto.FolderPath?.Trim() ?? string.Empty;
        entity.Notes = dto.Notes?.Trim() ?? string.Empty;
        entity.AttachmentPaths = dto.AttachmentPaths?
            .Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
        entity.ProjectPhases = dto.ProjectPhases?
            .Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();

        // ── Feature 1 — Due date ──────────────────────────────────────────
        entity.DueDate = dto.DueDate;

        // ── Feature 2 — Estimated hours ───────────────────────────────────
        entity.EstimatedHours = Math.Max(0, dto.EstimatedHours);

        // ── Feature 3 — Completion % ──────────────────────────────────────
        entity.CompletionPercent = Math.Clamp(dto.CompletionPercent, 0, 100);

        // ── Feature 4 — Tags ──────────────────────────────────────────────
        entity.Tags = dto.Tags?
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct()
            .ToList() ?? new List<string>();

        // ── Feature 5 — Color coding ──────────────────────────────────────
        entity.ColorHex = dto.ColorHex?.Trim() ?? string.Empty;

        // ── Feature 6 — Budget ────────────────────────────────────────────
        entity.Budget = dto.Budget.HasValue && dto.Budget.Value >= 0 ? dto.Budget : null;
        entity.BudgetCurrency = !string.IsNullOrWhiteSpace(dto.BudgetCurrency)
            ? dto.BudgetCurrency.Trim().ToUpperInvariant()
            : "USD";

        // ── Feature 7 — Linked URL ────────────────────────────────────────
        entity.ExternalUrl = dto.ExternalUrl?.Trim() ?? string.Empty;

        // ── Feature 8 — Template flag ─────────────────────────────────────
        entity.IsTemplate = dto.IsTemplate;

        // ── Feature 9 — Star ──────────────────────────────────────────────
        entity.IsStarred = dto.IsStarred;

        // ── Feature 10 — Recurrence rule ──────────────────────────────────
        entity.RecurrenceRule = dto.RecurrenceRule?.Trim() ?? string.Empty;
    }

    // ── Group B ───────────────────────────────────────────────────────────────

    public async Task<ServiceResult<ProjectRecord>> ArchiveProjectAsync(Guid id)
    {
        try
        {
            var project = await _repository.GetProjectByIdAsync(id);
            if (project == null)
                return ServiceResult<ProjectRecord>.FailureResult($"Project {id} not found");

            var oldStatus = project.Status;
            project.Status = "Archived";
            project.UpdatedAt = DateTime.Now;
            project.StatusHistory.Add(new StatusChange
            {
                FromStatus = oldStatus,
                ToStatus = "Archived",
                Reason = "Archived by user"
            });

            await _repository.SaveProjectAsync(project);
            _auditManager.RecordAudit("Project", id, BusinessLogic.Audit.AuditAction.Update,
                Environment.UserName, null,
                new Dictionary<string, object> { ["Status"] = "Archived" },
                "Project archived");

            return ServiceResult<ProjectRecord>.SuccessResult(project, "Project archived");
        }
        catch (Exception ex)
        {
            await _errorTrackingService.TrackErrorAsync(ex, "ArchiveProjectAsync");
            return ServiceResult<ProjectRecord>.FailureResult("Failed to archive project", ex.Message);
        }
    }

    public async Task<ServiceResult<ProjectRecord>> CloneProjectAsync(Guid sourceId, string? newName = null)
    {
        try
        {
            var source = await _repository.GetProjectByIdAsync(sourceId);
            if (source == null)
                return ServiceResult<ProjectRecord>.FailureResult($"Source project {sourceId} not found");

            var clone = new ProjectRecord
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ProjectName = newName?.Trim() ?? $"{source.ProjectName} (copy)",
                Client = source.Client,
                Status = "Active",
                Priority = source.Priority,
                FolderPath = string.Empty, // don't copy path
                Notes = source.Notes,
                AttachmentPaths = new List<string>(), // don't copy attachments
                ProjectPhases = new List<string>(source.ProjectPhases),
                Tags = new List<string>(source.Tags),
                ColorHex = source.ColorHex,
                Budget = source.Budget,
                BudgetCurrency = source.BudgetCurrency,
                ExternalUrl = source.ExternalUrl,
                IsTemplate = false,
                IsStarred = false,
                EstimatedHours = source.EstimatedHours,
                CompletionPercent = 0,
                RecurrenceRule = source.RecurrenceRule
            };

            await _repository.SaveProjectAsync(clone);
            _analyticsService.TrackEvent("ProjectCloned");
            return ServiceResult<ProjectRecord>.SuccessResult(clone, $"Cloned as '{clone.ProjectName}'");
        }
        catch (Exception ex)
        {
            await _errorTrackingService.TrackErrorAsync(ex, "CloneProjectAsync");
            return ServiceResult<ProjectRecord>.FailureResult("Failed to clone project", ex.Message);
        }
    }

    public async Task<ServiceResult<int>> BulkUpdateStatusAsync(IEnumerable<Guid> ids, string newStatus)
    {
        try
        {
            int count = 0;
            foreach (var id in ids)
            {
                var project = await _repository.GetProjectByIdAsync(id);
                if (project == null) continue;
                var old = project.Status;
                project.Status = newStatus;
                project.UpdatedAt = DateTime.Now;
                project.StatusHistory.Add(new StatusChange { FromStatus = old, ToStatus = newStatus, Reason = "Bulk update" });
                await _repository.SaveProjectAsync(project);
                count++;
            }
            _analyticsService.TrackEvent("BulkStatusUpdate", new Dictionary<string, object> { ["Count"] = count, ["Status"] = newStatus });
            return ServiceResult<int>.SuccessResult(count, $"Updated {count} projects to {newStatus}");
        }
        catch (Exception ex)
        {
            await _errorTrackingService.TrackErrorAsync(ex, "BulkUpdateStatusAsync");
            return ServiceResult<int>.FailureResult("Bulk update failed", ex.Message);
        }
    }

    public async Task<ServiceResult<int>> BulkDeleteAsync(IEnumerable<Guid> ids)
    {
        try
        {
            int count = 0;
            foreach (var id in ids)
            {
                var project = await _repository.GetProjectByIdAsync(id);
                if (project == null) continue;
                project.DeletedAt = DateTime.Now;
                await _repository.SaveProjectAsync(project);
                count++;
            }
            _analyticsService.TrackEvent("BulkDelete", new Dictionary<string, object> { ["Count"] = count });
            return ServiceResult<int>.SuccessResult(count, $"Soft-deleted {count} projects");
        }
        catch (Exception ex)
        {
            await _errorTrackingService.TrackErrorAsync(ex, "BulkDeleteAsync");
            return ServiceResult<int>.FailureResult("Bulk delete failed", ex.Message);
        }
    }

    public async Task<ServiceResult<List<ProjectRecord>>> GetDeletedProjectsAsync()
    {
        try
        {
            var all = await _repository.LoadProjectsAsync();
            return ServiceResult<List<ProjectRecord>>.SuccessResult(
                all.Where(p => p.IsDeleted).OrderByDescending(p => p.DeletedAt).ToList());
        }
        catch (Exception ex)
        {
            return ServiceResult<List<ProjectRecord>>.FailureResult("Failed to load deleted projects", ex.Message);
        }
    }

    public async Task<ServiceResult<ProjectRecord>> RestoreProjectAsync(Guid id)
    {
        try
        {
            var project = await _repository.GetProjectByIdAsync(id);
            if (project == null)
                return ServiceResult<ProjectRecord>.FailureResult($"Project {id} not found");

            project.DeletedAt = null;
            project.UpdatedAt = DateTime.Now;
            await _repository.SaveProjectAsync(project);
            return ServiceResult<ProjectRecord>.SuccessResult(project, "Project restored");
        }
        catch (Exception ex)
        {
            return ServiceResult<ProjectRecord>.FailureResult("Failed to restore project", ex.Message);
        }
    }

    public async Task<ServiceResult<int>> PurgeDeletedProjectsAsync()
    {
        try
        {
            var all = await _repository.LoadProjectsAsync();
            var deleted = all.Where(p => p.IsDeleted).ToList();
            foreach (var p in deleted)
                await _repository.DeleteProjectAsync(p.Id);
            return ServiceResult<int>.SuccessResult(deleted.Count, $"Purged {deleted.Count} projects");
        }
        catch (Exception ex)
        {
            return ServiceResult<int>.FailureResult("Purge failed", ex.Message);
        }
    }

    public async Task<ServiceResult<ProjectRecord>> ToggleStarAsync(Guid id)
    {
        try
        {
            var project = await _repository.GetProjectByIdAsync(id);
            if (project == null)
                return ServiceResult<ProjectRecord>.FailureResult($"Project {id} not found");

            project.IsStarred = !project.IsStarred;
            project.UpdatedAt = DateTime.Now;
            await _repository.SaveProjectAsync(project);
            return ServiceResult<ProjectRecord>.SuccessResult(project,
                project.IsStarred ? "Project starred" : "Star removed");
        }
        catch (Exception ex)
        {
            return ServiceResult<ProjectRecord>.FailureResult("Failed to toggle star", ex.Message);
        }
    }

    public async Task<ServiceResult<ProjectRecord>> SetCompletionAsync(Guid id, int percent)
    {
        try
        {
            var project = await _repository.GetProjectByIdAsync(id);
            if (project == null)
                return ServiceResult<ProjectRecord>.FailureResult($"Project {id} not found");

            project.CompletionPercent = Math.Clamp(percent, 0, 100);
            if (percent >= 100 && project.Status != "Completed")
            {
                var old = project.Status;
                project.Status = "Completed";
                project.StatusHistory.Add(new StatusChange { FromStatus = old, ToStatus = "Completed", Reason = "Completion reached 100%" });
            }
            project.UpdatedAt = DateTime.Now;
            await _repository.SaveProjectAsync(project);
            return ServiceResult<ProjectRecord>.SuccessResult(project, $"Completion set to {percent}%");
        }
        catch (Exception ex)
        {
            return ServiceResult<ProjectRecord>.FailureResult("Failed to set completion", ex.Message);
        }
    }

    public async Task<ServiceResult<ProjectRecord>> CreateFromTemplateAsync(Guid templateId, string newName)
    {
        try
        {
            var template = await _repository.GetProjectByIdAsync(templateId);
            if (template == null)
                return ServiceResult<ProjectRecord>.FailureResult("Template not found");
            if (!template.IsTemplate)
                return ServiceResult<ProjectRecord>.FailureResult("Project is not marked as a template");

            return await CloneProjectAsync(templateId, newName.Trim());
        }
        catch (Exception ex)
        {
            return ServiceResult<ProjectRecord>.FailureResult("Failed to create from template", ex.Message);
        }
    }

    // ── Group C ───────────────────────────────────────────────────────────────

    public async Task<ServiceResult<List<ProjectRecord>>> GetProjectsByDateRangeAsync(DateTime? from, DateTime? to, bool useDueDate = false)
    {
        try
        {
            var all = await _repository.LoadProjectsAsync();
            var q = all.Where(p => !p.IsDeleted).AsEnumerable();

            if (useDueDate)
            {
                if (from.HasValue) q = q.Where(p => p.DueDate.HasValue && p.DueDate.Value >= from.Value);
                if (to.HasValue)   q = q.Where(p => p.DueDate.HasValue && p.DueDate.Value <= to.Value);
            }
            else
            {
                if (from.HasValue) q = q.Where(p => p.CreatedAt >= from.Value);
                if (to.HasValue)   q = q.Where(p => p.CreatedAt <= to.Value);
            }

            return ServiceResult<List<ProjectRecord>>.SuccessResult(q.OrderByDescending(p => p.UpdatedAt).ToList());
        }
        catch (Exception ex)
        {
            return ServiceResult<List<ProjectRecord>>.FailureResult("Date range filter failed", ex.Message);
        }
    }

    public async Task<ServiceResult<List<ProjectRecord>>> GetProjectsByTagsAsync(IEnumerable<string> tags, bool andMode = false)
    {
        try
        {
            var tagList = tags.Select(t => t.ToLowerInvariant()).ToList();
            var all = await _repository.LoadProjectsAsync();
            var q = all.Where(p => !p.IsDeleted);

            var result = andMode
                ? q.Where(p => tagList.All(t => p.Tags.Contains(t)))
                : q.Where(p => p.Tags.Any(t => tagList.Contains(t)));

            return ServiceResult<List<ProjectRecord>>.SuccessResult(result.ToList());
        }
        catch (Exception ex)
        {
            return ServiceResult<List<ProjectRecord>>.FailureResult("Tag filter failed", ex.Message);
        }
    }

    public async Task<ServiceResult<List<ProjectRecord>>> GetProjectsByClientAsync(string clientName)
    {
        try
        {
            var all = await _repository.LoadProjectsAsync();
            var result = all.Where(p => !p.IsDeleted &&
                p.Client.Contains(clientName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.UpdatedAt)
                .ToList();
            return ServiceResult<List<ProjectRecord>>.SuccessResult(result);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<ProjectRecord>>.FailureResult("Client filter failed", ex.Message);
        }
    }

    public async Task<ServiceResult<List<ProjectRecord>>> GetProjectsByPresetAsync(string presetName)
    {
        var all = await _repository.LoadProjectsAsync();
        var active = all.Where(p => !p.IsDeleted);

        var result = presetName.ToLowerInvariant() switch
        {
            "active+high" or "high_active" => active.Where(p => p.Status == "Active" && p.Priority == "High"),
            "overdue"                       => active.Where(p => p.IsOverdue),
            "due-today"                     => active.Where(p => p.IsDueToday),
            "starred"                       => active.Where(p => p.IsStarred),
            "templates"                     => active.Where(p => p.IsTemplate),
            "no-folder"                     => active.Where(p => string.IsNullOrWhiteSpace(p.FolderPath)),
            _                               => active
        };

        return ServiceResult<List<ProjectRecord>>.SuccessResult(result.OrderByDescending(p => p.UpdatedAt).ToList());
    }

    public async Task<ServiceResult<List<ProjectRecord>>> GetOverdueProjectsAsync()
    {
        var all = await _repository.LoadProjectsAsync();
        return ServiceResult<List<ProjectRecord>>.SuccessResult(
            all.Where(p => !p.IsDeleted && p.IsOverdue).OrderBy(p => p.DueDate).ToList());
    }

    public async Task<ServiceResult<List<ProjectRecord>>> GetDueTodayProjectsAsync()
    {
        var all = await _repository.LoadProjectsAsync();
        return ServiceResult<List<ProjectRecord>>.SuccessResult(
            all.Where(p => !p.IsDeleted && p.IsDueToday).ToList());
    }

    public async Task<ServiceResult<List<string>>> GetUniqueClientsAsync()
    {
        var all = await _repository.LoadProjectsAsync();
        return ServiceResult<List<string>>.SuccessResult(
            all.Where(p => !p.IsDeleted && !string.IsNullOrWhiteSpace(p.Client))
               .Select(p => p.Client).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList());
    }

    public async Task<ServiceResult<List<string>>> GetUniqueTagsAsync()
    {
        var all = await _repository.LoadProjectsAsync();
        return ServiceResult<List<string>>.SuccessResult(
            all.Where(p => !p.IsDeleted)
               .SelectMany(p => p.Tags).Distinct().OrderBy(t => t).ToList());
    }

    public async Task<ServiceResult<Dictionary<string, int>>> GetProjectCountByStatusAsync()
    {
        var all = await _repository.LoadProjectsAsync();
        return ServiceResult<Dictionary<string, int>>.SuccessResult(
            all.Where(p => !p.IsDeleted)
               .GroupBy(p => p.Status)
               .ToDictionary(g => g.Key, g => g.Count()));
    }

    public async Task<ServiceResult<Dictionary<string, int>>> GetProjectCountByClientAsync()
    {
        var all = await _repository.LoadProjectsAsync();
        return ServiceResult<Dictionary<string, int>>.SuccessResult(
            all.Where(p => !p.IsDeleted && !string.IsNullOrWhiteSpace(p.Client))
               .GroupBy(p => p.Client, StringComparer.OrdinalIgnoreCase)
               .ToDictionary(g => g.Key, g => g.Count()));
    }

    // ── Group D ───────────────────────────────────────────────────────────────

    public async Task<ServiceResult<bool>> ExportToCsvAsync(IEnumerable<ProjectRecord> projects, string filePath, char separator = ',')
    {
        try
        {
            var lines = new List<string>
            {
                string.Join(separator, "Id","ProjectName","Client","Status","Priority",
                    "DueDate","CompletionPercent","EstimatedHours","Tags","Budget",
                    "BudgetCurrency","ExternalUrl","ColorHex","IsStarred","IsTemplate",
                    "FolderPath","Notes","CreatedAt","UpdatedAt")
            };

            foreach (var p in projects)
            {
                lines.Add(string.Join(separator,
                    Csv(p.Id.ToString()), Csv(p.ProjectName), Csv(p.Client), Csv(p.Status), Csv(p.Priority),
                    Csv(p.DueDate?.ToString("yyyy-MM-dd") ?? ""), Csv(p.CompletionPercent.ToString()),
                    Csv(p.EstimatedHours.ToString("F2")), Csv(string.Join("|", p.Tags)),
                    Csv(p.Budget?.ToString("F2") ?? ""), Csv(p.BudgetCurrency),
                    Csv(p.ExternalUrl), Csv(p.ColorHex), Csv(p.IsStarred.ToString()),
                    Csv(p.IsTemplate.ToString()), Csv(p.FolderPath), Csv(p.Notes),
                    Csv(p.CreatedAt.ToString("yyyy-MM-dd HH:mm")), Csv(p.UpdatedAt.ToString("yyyy-MM-dd HH:mm"))));
            }

            await File.WriteAllLinesAsync(filePath, lines, System.Text.Encoding.UTF8);
            _analyticsService.TrackExport("CSV", lines.Count - 1);
            return ServiceResult<bool>.SuccessResult(true, $"Exported {lines.Count - 1} projects to CSV");
        }
        catch (Exception ex)
        {
            await _errorTrackingService.TrackErrorAsync(ex, "ExportToCsvAsync");
            return ServiceResult<bool>.FailureResult("CSV export failed", ex.Message);
        }
    }

    private static string Csv(string? value)
    {
        if (value == null) return "\"\"";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    public async Task<ServiceResult<string>> ExportToJsonAsync(IEnumerable<ProjectRecord> projects)
    {
        try
        {
            var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(projects.ToList(), opts);
            _analyticsService.TrackExport("JSON", projects.Count());
            return ServiceResult<string>.SuccessResult(json, "Exported to JSON");
        }
        catch (Exception ex)
        {
            return ServiceResult<string>.FailureResult("JSON export failed", ex.Message);
        }
    }

    public async Task<ServiceResult<int>> ImportFromCsvAsync(string filePath, bool skipDuplicates = true)
    {
        try
        {
            if (!File.Exists(filePath))
                return ServiceResult<int>.FailureResult("File not found");

            var lines = await File.ReadAllLinesAsync(filePath);
            if (lines.Length < 2)
                return ServiceResult<int>.SuccessResult(0, "No data rows found");

            int imported = 0;
            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = ParseCsvLine(lines[i]);
                if (cols.Length < 5) continue;

                var dto = new ProjectDto
                {
                    ProjectName = cols.Length > 1 ? cols[1] : "",
                    Client = cols.Length > 2 ? cols[2] : "",
                    Status = cols.Length > 3 ? cols[3] : "Active",
                    Priority = cols.Length > 4 ? cols[4] : "Normal",
                    DueDate = cols.Length > 5 && DateTime.TryParse(cols[5], out var d) ? d : null,
                    CompletionPercent = cols.Length > 6 && int.TryParse(cols[6], out var cp) ? cp : 0,
                    EstimatedHours = cols.Length > 7 && decimal.TryParse(cols[7], out var eh) ? eh : 0,
                    Tags = cols.Length > 8 ? cols[8].Split('|').Where(t => !string.IsNullOrWhiteSpace(t)).ToList() : new(),
                    FolderPath = cols.Length > 14 ? cols[14] : "",
                    Notes = cols.Length > 15 ? cols[15] : ""
                };

                if (skipDuplicates)
                {
                    var existing = await _repository.ProjectNameExistsAsync(dto.ProjectName);
                    if (existing) continue;
                }

                var result = await CreateProjectAsync(dto);
                if (result.Success) imported++;
            }

            return ServiceResult<int>.SuccessResult(imported, $"Imported {imported} projects from CSV");
        }
        catch (Exception ex)
        {
            await _errorTrackingService.TrackErrorAsync(ex, "ImportFromCsvAsync");
            return ServiceResult<int>.FailureResult("CSV import failed", ex.Message);
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    public async Task<ServiceResult<int>> ImportFromJsonAsync(string jsonData, bool overwriteExisting = false)
    {
        try
        {
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var records = System.Text.Json.JsonSerializer.Deserialize<List<ProjectRecord>>(jsonData, opts);
            if (records == null) return ServiceResult<int>.FailureResult("Invalid JSON");

            int imported = 0;
            foreach (var record in records)
            {
                if (!overwriteExisting)
                {
                    var exists = await _repository.ProjectNameExistsAsync(record.ProjectName);
                    if (exists) continue;
                    record.Id = Guid.NewGuid(); // ensure unique ID
                }
                record.UpdatedAt = DateTime.Now;
                await _repository.SaveProjectAsync(record);
                imported++;
            }

            return ServiceResult<int>.SuccessResult(imported, $"Imported {imported} projects from JSON");
        }
        catch (Exception ex)
        {
            await _errorTrackingService.TrackErrorAsync(ex, "ImportFromJsonAsync");
            return ServiceResult<int>.FailureResult("JSON import failed", ex.Message);
        }
    }

    public async Task<ServiceResult<int>> ImportFromExcelAsync(string filePath, bool skipDuplicates = true)
    {
        try
        {
            if (!File.Exists(filePath))
                return ServiceResult<int>.FailureResult("File not found");

            using var workbook = new ClosedXML.Excel.XLWorkbook(filePath);
            var ws = workbook.Worksheet(1);
            var rows = ws.RowsUsed().Skip(1).ToList(); // skip header

            int imported = 0;
            foreach (var row in rows)
            {
                var dto = new ProjectDto
                {
                    ProjectName = row.Cell(1).GetString(),
                    Client = row.Cell(2).GetString(),
                    Status = row.Cell(3).GetString().IfEmpty("Active"),
                    Priority = row.Cell(4).GetString().IfEmpty("Normal"),
                    Notes = row.Cell(5).GetString(),
                    FolderPath = row.Cell(6).GetString()
                };

                if (string.IsNullOrWhiteSpace(dto.ProjectName)) continue;
                if (skipDuplicates && await _repository.ProjectNameExistsAsync(dto.ProjectName)) continue;

                var result = await CreateProjectAsync(dto);
                if (result.Success) imported++;
            }

            return ServiceResult<int>.SuccessResult(imported, $"Imported {imported} projects from Excel");
        }
        catch (Exception ex)
        {
            await _errorTrackingService.TrackErrorAsync(ex, "ImportFromExcelAsync");
            return ServiceResult<int>.FailureResult("Excel import failed", ex.Message);
        }
    }

    // ── Group J — Backup & Maintenance ────────────────────────────────────────

    public async Task<ServiceResult<string>> CreateBackupAsync(string? backupDirectory = null)
    {
        try
        {
            var config = Gradual.Infrastructure.ServiceContainer
                .GetRequiredService<Services.ConfigurationService>();
            var dataDir = System.IO.Path.Combine(AppContext.BaseDirectory,
                config.Settings.ApplicationSettings.DataDirectory);
            var backupDir = backupDirectory ?? System.IO.Path.Combine(AppContext.BaseDirectory,
                config.Settings.ApplicationSettings.BackupDirectory);

            Directory.CreateDirectory(backupDir);
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zipPath = System.IO.Path.Combine(backupDir, $"backup_{ts}.zip");

            await Task.Run(() =>
                System.IO.Compression.ZipFile.CreateFromDirectory(dataDir, zipPath));

            _loggingService.LogInformation("Backup created: {ZipPath}", zipPath);
            return ServiceResult<string>.SuccessResult(zipPath, $"Backup created: {zipPath}");
        }
        catch (Exception ex)
        {
            await _errorTrackingService.TrackErrorAsync(ex, "CreateBackupAsync");
            return ServiceResult<string>.FailureResult("Backup failed", ex.Message);
        }
    }

    public async Task<ServiceResult<bool>> RestoreFromBackupAsync(string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath))
                return ServiceResult<bool>.FailureResult("Backup file not found");

            var config = Gradual.Infrastructure.ServiceContainer
                .GetRequiredService<Services.ConfigurationService>();
            var dataDir = System.IO.Path.Combine(AppContext.BaseDirectory,
                config.Settings.ApplicationSettings.DataDirectory);

            // Safety: create a backup of current data first
            var safetyDir = System.IO.Path.Combine(AppContext.BaseDirectory,
                config.Settings.ApplicationSettings.BackupDirectory, "pre-restore");
            Directory.CreateDirectory(safetyDir);
            if (Directory.Exists(dataDir))
                await Task.Run(() => System.IO.Compression.ZipFile.CreateFromDirectory(
                    dataDir, System.IO.Path.Combine(safetyDir, $"pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}.zip")));

            // Restore
            if (Directory.Exists(dataDir))
                Directory.Delete(dataDir, recursive: true);
            await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(backupPath, dataDir));

            _loggingService.LogInformation("Restored from backup: {BackupPath}", backupPath);
            return ServiceResult<bool>.SuccessResult(true, "Restore completed. Restart the application.");
        }
        catch (Exception ex)
        {
            await _errorTrackingService.TrackErrorAsync(ex, "RestoreFromBackupAsync");
            return ServiceResult<bool>.FailureResult("Restore failed", ex.Message);
        }
    }

    public async Task<ServiceResult<List<string>>> CheckDataIntegrityAsync()
    {
        var issues = new List<string>();
        try
        {
            var all = await _repository.LoadProjectsAsync();

            // Check for duplicate IDs
            var dupeIds = all.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var id in dupeIds)
                issues.Add($"Duplicate ID: {id}");

            // Check for duplicate names (among non-deleted)
            var dupeNames = all.Where(p => !p.IsDeleted)
                .GroupBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var name in dupeNames)
                issues.Add($"Duplicate project name: '{name}'");

            // Check for invalid completion percent
            foreach (var p in all.Where(p => p.CompletionPercent < 0 || p.CompletionPercent > 100))
                issues.Add($"Invalid completion {p.CompletionPercent}% on '{p.ProjectName}'");

            // Check for missing folder paths on active projects
            foreach (var p in all.Where(p => !p.IsDeleted && p.Status == "Active" && !string.IsNullOrWhiteSpace(p.FolderPath) && !Directory.Exists(p.FolderPath)))
                issues.Add($"Folder not found for '{p.ProjectName}': {p.FolderPath}");

            // Check for negative budgets
            foreach (var p in all.Where(p => p.Budget.HasValue && p.Budget.Value < 0))
                issues.Add($"Negative budget on '{p.ProjectName}'");

            return ServiceResult<List<string>>.SuccessResult(issues,
                issues.Count == 0 ? "Data integrity OK" : $"{issues.Count} issue(s) found");
        }
        catch (Exception ex)
        {
            return ServiceResult<List<string>>.FailureResult("Integrity check failed", ex.Message);
        }
    }

    // ── Group J — Recurrence ──────────────────────────────────────────────────

    public async Task<ServiceResult<int>> ProcessRecurringProjectsAsync()
    {
        try
        {
            var all = await _repository.LoadProjectsAsync();
            var recurring = all.Where(p => !p.IsDeleted
                && !string.IsNullOrWhiteSpace(p.RecurrenceRule)
                && p.DueDate.HasValue
                && p.DueDate.Value.Date < DateTime.Today
                && p.Status != "Completed").ToList();

            int cloned = 0;
            foreach (var project in recurring)
            {
                // Simple FREQ parsing: FREQ=WEEKLY → add 7 days, FREQ=MONTHLY → add 1 month
                var nextDue = ComputeNextOccurrence(project.DueDate!.Value, project.RecurrenceRule);
                if (nextDue == null) continue;

                var clone = await CloneProjectAsync(project.Id, project.ProjectName);
                if (clone.Success && clone.Data != null)
                {
                    clone.Data.DueDate = nextDue;
                    clone.Data.CompletionPercent = 0;
                    clone.Data.Status = "Active";
                    await _repository.SaveProjectAsync(clone.Data);
                    cloned++;
                }

                // Mark original as completed
                project.Status = "Completed";
                project.UpdatedAt = DateTime.Now;
                await _repository.SaveProjectAsync(project);
            }

            return ServiceResult<int>.SuccessResult(cloned, $"Processed {cloned} recurring project(s)");
        }
        catch (Exception ex)
        {
            return ServiceResult<int>.FailureResult("Recurrence processing failed", ex.Message);
        }
    }

    private static DateTime? ComputeNextOccurrence(DateTime current, string rrule)
    {
        if (rrule.Contains("FREQ=DAILY"))  return current.AddDays(1);
        if (rrule.Contains("FREQ=WEEKLY")) return current.AddDays(7);
        if (rrule.Contains("FREQ=MONTHLY"))return current.AddMonths(1);
        if (rrule.Contains("FREQ=YEARLY")) return current.AddYears(1);
        return null;
    }
}

