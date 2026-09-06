namespace Gradual.Models.DTOs;

/// <summary>
/// Data Transfer Object for Project operations.
/// Separates domain model from UI/API layer.
/// All new fields mirror ProjectRecord so the mapping helper stays in sync.
/// </summary>
public class ProjectDto
{
    // ── Original fields ───────────────────────────────────────────────────────
    public string ProjectName { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string Priority { get; set; } = "Normal";
    public string FolderPath { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<string> AttachmentPaths { get; set; } = new();
    public List<string> ProjectPhases { get; set; } = new();

    // ── Feature 1 — Due date ─────────────────────────────────────────────────
    public DateTime? DueDate { get; set; }

    // ── Feature 2 — Estimated hours ──────────────────────────────────────────
    public decimal EstimatedHours { get; set; } = 0m;

    // ── Feature 3 — Completion % ─────────────────────────────────────────────
    public int CompletionPercent { get; set; } = 0;

    // ── Feature 4 — Tags ─────────────────────────────────────────────────────
    public List<string> Tags { get; set; } = new();

    // ── Feature 5 — Color coding ─────────────────────────────────────────────
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
    public string RecurrenceRule { get; set; } = string.Empty;

    // ── Status-change reason (used by UpdateProjectAsync when status changes) ─
    public string StatusChangeReason { get; set; } = string.Empty;
}

