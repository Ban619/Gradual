namespace Gradual.BusinessLogic.Audit;

/// <summary>
/// Manages audit trail and change tracking for compliance and history
/// </summary>
public class AuditTrailManager
{
    private readonly List<AuditEntry> _auditLog = new();
    private readonly List<ChangeRecord> _changeHistory = new();

    /// <summary>
    /// Records an audit entry
    /// </summary>
    public AuditEntry RecordAudit(
        string entityType,
        Guid entityId,
        AuditAction action,
        string performedBy,
        Dictionary<string, object>? oldValues = null,
        Dictionary<string, object>? newValues = null,
        string? notes = null)
    {
        var entry = new AuditEntry
        {
            AuditId = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            PerformedBy = performedBy,
            PerformedDate = DateTime.Now,
            OldValues = oldValues ?? new Dictionary<string, object>(),
            NewValues = newValues ?? new Dictionary<string, object>(),
            Notes = notes ?? string.Empty,
            IpAddress = GetCurrentIpAddress(),
            UserAgent = GetCurrentUserAgent()
        };

        _auditLog.Add(entry);

        // Create detailed change records
        if (action == AuditAction.Update && oldValues != null && newValues != null)
        {
            CreateChangeRecords(entry, oldValues, newValues);
        }

        return entry;
    }

    /// <summary>
    /// Creates detailed change records for field-level tracking
    /// </summary>
    private void CreateChangeRecords(
        AuditEntry auditEntry,
        Dictionary<string, object> oldValues,
        Dictionary<string, object> newValues)
    {
        foreach (var key in newValues.Keys)
        {
            if (!oldValues.ContainsKey(key))
            {
                // New field added
                _changeHistory.Add(new ChangeRecord
                {
                    ChangeId = Guid.NewGuid(),
                    AuditId = auditEntry.AuditId,
                    EntityType = auditEntry.EntityType,
                    EntityId = auditEntry.EntityId,
                    FieldName = key,
                    OldValue = null,
                    NewValue = newValues[key]?.ToString(),
                    ChangeType = ChangeType.Added,
                    ChangedBy = auditEntry.PerformedBy,
                    ChangedDate = auditEntry.PerformedDate
                });
            }
            else if (!Equals(oldValues[key], newValues[key]))
            {
                // Field modified
                _changeHistory.Add(new ChangeRecord
                {
                    ChangeId = Guid.NewGuid(),
                    AuditId = auditEntry.AuditId,
                    EntityType = auditEntry.EntityType,
                    EntityId = auditEntry.EntityId,
                    FieldName = key,
                    OldValue = oldValues[key]?.ToString(),
                    NewValue = newValues[key]?.ToString(),
                    ChangeType = ChangeType.Modified,
                    ChangedBy = auditEntry.PerformedBy,
                    ChangedDate = auditEntry.PerformedDate
                });
            }
        }

        // Check for removed fields
        foreach (var key in oldValues.Keys)
        {
            if (!newValues.ContainsKey(key))
            {
                _changeHistory.Add(new ChangeRecord
                {
                    ChangeId = Guid.NewGuid(),
                    AuditId = auditEntry.AuditId,
                    EntityType = auditEntry.EntityType,
                    EntityId = auditEntry.EntityId,
                    FieldName = key,
                    OldValue = oldValues[key]?.ToString(),
                    NewValue = null,
                    ChangeType = ChangeType.Removed,
                    ChangedBy = auditEntry.PerformedBy,
                    ChangedDate = auditEntry.PerformedDate
                });
            }
        }
    }

    /// <summary>
    /// Gets audit trail for a specific entity
    /// </summary>
    public List<AuditEntry> GetEntityAuditTrail(Guid entityId, string? entityType = null)
    {
        var query = _auditLog.Where(a => a.EntityId == entityId);

        if (!string.IsNullOrEmpty(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        return query.OrderByDescending(a => a.PerformedDate).ToList();
    }

    /// <summary>
    /// Gets change history for a specific field
    /// </summary>
    public List<ChangeRecord> GetFieldHistory(Guid entityId, string fieldName)
    {
        return _changeHistory
            .Where(c => c.EntityId == entityId && c.FieldName == fieldName)
            .OrderByDescending(c => c.ChangedDate)
            .ToList();
    }

    /// <summary>
    /// Gets all changes by a specific user
    /// </summary>
    public List<AuditEntry> GetUserActivity(string username, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _auditLog.Where(a => a.PerformedBy == username);

        if (startDate.HasValue)
        {
            query = query.Where(a => a.PerformedDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.PerformedDate <= endDate.Value);
        }

        return query.OrderByDescending(a => a.PerformedDate).ToList();
    }

    /// <summary>
    /// Generates compliance report
    /// </summary>
    public ComplianceReport GenerateComplianceReport(DateTime startDate, DateTime endDate)
    {
        var entries = _auditLog
            .Where(a => a.PerformedDate >= startDate && a.PerformedDate <= endDate)
            .ToList();

        var report = new ComplianceReport
        {
            StartDate = startDate,
            EndDate = endDate,
            GeneratedDate = DateTime.Now,
            TotalActions = entries.Count,
            ActionsByType = entries.GroupBy(e => e.Action)
                .ToDictionary(g => g.Key, g => g.Count()),
            ActionsByUser = entries.GroupBy(e => e.PerformedBy)
                .ToDictionary(g => g.Key, g => g.Count()),
            ActionsByEntity = entries.GroupBy(e => e.EntityType)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        // Identify suspicious activities
        report.SuspiciousActivities = IdentifySuspiciousActivities(entries);

        return report;
    }

    /// <summary>
    /// Identifies potentially suspicious audit activities
    /// </summary>
    private List<SuspiciousActivity> IdentifySuspiciousActivities(List<AuditEntry> entries)
    {
        var suspicious = new List<SuspiciousActivity>();

        // Check for bulk deletions
        var deletions = entries.Where(e => e.Action == AuditAction.Delete).ToList();
        var bulkDeletions = deletions
            .GroupBy(d => new { d.PerformedBy, Date = d.PerformedDate.Date })
            .Where(g => g.Count() > 10);

        foreach (var group in bulkDeletions)
        {
            suspicious.Add(new SuspiciousActivity
            {
                ActivityType = "Bulk Deletion",
                Description = $"{group.Key.PerformedBy} deleted {group.Count()} items on {group.Key.Date:yyyy-MM-dd}",
                Severity = SuspiciousActivitySeverity.High,
                RelatedAuditIds = group.Select(e => e.AuditId).ToList()
            });
        }

        // Check for after-hours activity
        var afterHours = entries.Where(e =>
            e.PerformedDate.Hour < 6 || e.PerformedDate.Hour > 22);

        if (afterHours.Count() > 5)
        {
            var byUser = afterHours.GroupBy(e => e.PerformedBy);
            foreach (var userGroup in byUser.Where(g => g.Count() > 5))
            {
                suspicious.Add(new SuspiciousActivity
                {
                    ActivityType = "After Hours Access",
                    Description = $"{userGroup.Key} performed {userGroup.Count()} actions outside business hours",
                    Severity = SuspiciousActivitySeverity.Medium,
                    RelatedAuditIds = userGroup.Select(e => e.AuditId).ToList()
                });
            }
        }

        // Check for rapid successive changes
        var rapidChanges = entries
            .OrderBy(e => e.PerformedDate)
            .ToList();

        for (int i = 0; i < rapidChanges.Count - 10; i++)
        {
            var timeSpan = rapidChanges[i + 9].PerformedDate - rapidChanges[i].PerformedDate;
            if (timeSpan.TotalMinutes < 5)
            {
                suspicious.Add(new SuspiciousActivity
                {
                    ActivityType = "Rapid Changes",
                    Description = $"10+ actions within 5 minutes by {rapidChanges[i].PerformedBy}",
                    Severity = SuspiciousActivitySeverity.Low,
                    RelatedAuditIds = rapidChanges.Skip(i).Take(10).Select(e => e.AuditId).ToList()
                });
                break; // Only report once
            }
        }

        return suspicious;
    }

    /// <summary>
    /// Exports audit trail for external review
    /// </summary>
    public AuditExport ExportAuditTrail(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _auditLog.AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(a => a.PerformedDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.PerformedDate <= endDate.Value);
        }

        var entries = query.OrderBy(a => a.PerformedDate).ToList();

        return new AuditExport
        {
            ExportDate = DateTime.Now,
            StartDate = startDate ?? entries.FirstOrDefault()?.PerformedDate ?? DateTime.Now,
            EndDate = endDate ?? entries.LastOrDefault()?.PerformedDate ?? DateTime.Now,
            TotalEntries = entries.Count,
            Entries = entries
        };
    }

    /// <summary>
    /// Searches audit log
    /// </summary>
    public List<AuditEntry> SearchAuditLog(AuditSearchCriteria criteria)
    {
        var query = _auditLog.AsQueryable();

        if (!string.IsNullOrEmpty(criteria.EntityType))
        {
            query = query.Where(a => a.EntityType == criteria.EntityType);
        }

        if (criteria.EntityId.HasValue)
        {
            query = query.Where(a => a.EntityId == criteria.EntityId.Value);
        }

        if (!string.IsNullOrEmpty(criteria.PerformedBy))
        {
            query = query.Where(a => a.PerformedBy.Contains(criteria.PerformedBy));
        }

        if (criteria.Action.HasValue)
        {
            query = query.Where(a => a.Action == criteria.Action.Value);
        }

        if (criteria.StartDate.HasValue)
        {
            query = query.Where(a => a.PerformedDate >= criteria.StartDate.Value);
        }

        if (criteria.EndDate.HasValue)
        {
            query = query.Where(a => a.PerformedDate <= criteria.EndDate.Value);
        }

        if (!string.IsNullOrEmpty(criteria.SearchText))
        {
            query = query.Where(a => a.Notes.Contains(criteria.SearchText));
        }

        return query.OrderByDescending(a => a.PerformedDate)
                   .Take(criteria.MaxResults)
                   .ToList();
    }

    /// <summary>
    /// Gets audit statistics
    /// </summary>
    public AuditStatistics GetStatistics(DateTime startDate, DateTime endDate)
    {
        var entries = _auditLog
            .Where(a => a.PerformedDate >= startDate && a.PerformedDate <= endDate)
            .ToList();

        var stats = new AuditStatistics
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalActions = entries.Count,
            UniqueUsers = entries.Select(e => e.PerformedBy).Distinct().Count(),
            UniqueEntities = entries.Select(e => e.EntityId).Distinct().Count(),
            ActionBreakdown = entries.GroupBy(e => e.Action)
                .ToDictionary(g => g.Key, g => g.Count()),
            MostActiveUsers = entries.GroupBy(e => e.PerformedBy)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count()),
            DailyActivity = entries.GroupBy(e => e.PerformedDate.Date)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return stats;
    }

    private string GetCurrentIpAddress()
    {
        // In production, get from HttpContext
        return "127.0.0.1";
    }

    private string GetCurrentUserAgent()
    {
        // In production, get from HttpContext
        return "Gradual Client";
    }
}

/// <summary>
/// Audit entry
/// </summary>
public class AuditEntry
{
    public Guid AuditId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime PerformedDate { get; set; }
    public Dictionary<string, object> OldValues { get; set; } = new();
    public Dictionary<string, object> NewValues { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}

/// <summary>
/// Change record for field-level tracking
/// </summary>
public class ChangeRecord
{
    public Guid ChangeId { get; set; }
    public Guid AuditId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public ChangeType ChangeType { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedDate { get; set; }
}

public enum AuditAction
{
    Create,
    Read,
    Update,
    Delete,
    Login,
    Logout,
    Export,
    Import,
    Approve,
    Reject
}

public enum ChangeType
{
    Added,
    Modified,
    Removed
}

public class ComplianceReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedDate { get; set; }
    public int TotalActions { get; set; }
    public Dictionary<AuditAction, int> ActionsByType { get; set; } = new();
    public Dictionary<string, int> ActionsByUser { get; set; } = new();
    public Dictionary<string, int> ActionsByEntity { get; set; } = new();
    public List<SuspiciousActivity> SuspiciousActivities { get; set; } = new();
}

public class SuspiciousActivity
{
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SuspiciousActivitySeverity Severity { get; set; }
    public List<Guid> RelatedAuditIds { get; set; } = new();
}

public enum SuspiciousActivitySeverity
{
    Low,
    Medium,
    High,
    Critical
}

public class AuditExport
{
    public DateTime ExportDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalEntries { get; set; }
    public List<AuditEntry> Entries { get; set; } = new();
}

public class AuditSearchCriteria
{
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? PerformedBy { get; set; }
    public AuditAction? Action { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SearchText { get; set; }
    public int MaxResults { get; set; } = 100;
}

public class AuditStatistics
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalActions { get; set; }
    public int UniqueUsers { get; set; }
    public int UniqueEntities { get; set; }
    public Dictionary<AuditAction, int> ActionBreakdown { get; set; } = new();
    public Dictionary<string, int> MostActiveUsers { get; set; } = new();
    public Dictionary<DateTime, int> DailyActivity { get; set; } = new();
}
