namespace Gradual.Models;

/// <summary>
/// Core project entity. All new fields are nullable or have safe defaults so
/// existing JSON data files load correctly without migration.
/// </summary>
public class ProjectRecord
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public Guid Id { get; set; } = Guid.NewGuid();

    // ── Core fields (original) ────────────────────────────────────────────────
    public string ProjectName { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string Priority { get; set; } = "Normal";
    public string FolderPath { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<string> AttachmentPaths { get; set; } = new();
    public List<string> ProjectPhases { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // ── Feature 1 — Due date ─────────────────────────────────────────────────
    public DateTime? DueDate { get; set; }

    // ── Feature 2 — Estimated hours ──────────────────────────────────────────
    public decimal EstimatedHours { get; set; } = 0m;

    // ── Feature 3 — Completion % ─────────────────────────────────────────────
    /// <summary>0–100 inclusive.</summary>
    public int CompletionPercent { get; set; } = 0;

    // ── Feature 4 — Tags ─────────────────────────────────────────────────────
    public List<string> Tags { get; set; } = new();

    // ── Feature 5 — Color coding ─────────────────────────────────────────────
    /// <summary>HTML hex color, e.g. "#3B82F6". Empty = use default status color.</summary>
    public string ColorHex { get; set; } = string.Empty;

    // ── Feature 6 — Budget ───────────────────────────────────────────────────
    public decimal? Budget { get; set; }
    public string BudgetCurrency { get; set; } = "USD";

    // ── Feature 7 — Linked URL ───────────────────────────────────────────────
    public string ExternalUrl { get; set; } = string.Empty;

    // ── Feature 8 — Template flag ────────────────────────────────────────────
    public bool IsTemplate { get; set; } = false;

    // ── Feature 9 — Star / favourite ─────────────────────────────────────────
    public bool IsStarred { get; set; } = false;

    // ── Feature 10 — Recurrence rule ─────────────────────────────────────────
    /// <summary>
    /// RFC-5545 RRULE string, e.g. "FREQ=WEEKLY;BYDAY=MO". Empty = no recurrence.
    /// When a due date passes, the service clones this project with the next occurrence.
    /// </summary>
    public string RecurrenceRule { get; set; } = string.Empty;

    // ── Feature 16 — Soft delete ─────────────────────────────────────────────
    public DateTime? DeletedAt { get; set; }

    // ── Feature 19 — Status change history ───────────────────────────────────
    public List<StatusChange> StatusHistory { get; set; } = new();

    // ── Computed helpers ──────────────────────────────────────────────────────
    public bool IsDeleted => DeletedAt.HasValue;
    public bool IsOverdue => DueDate.HasValue && DueDate.Value.Date < DateTime.Today && Status != "Completed";
    public bool IsDueToday => DueDate.HasValue && DueDate.Value.Date == DateTime.Today;
    public int DaysUntilDue => DueDate.HasValue ? (DueDate.Value.Date - DateTime.Today).Days : int.MaxValue;
}

/// <summary>
/// Immutable record of a single status transition (Feature 19).
/// </summary>
public class StatusChange
{
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.Now;
    public string ChangedBy { get; set; } = Environment.UserName;
    public string Reason { get; set; } = string.Empty;
}

