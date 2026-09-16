using System.Text.Json;
using Gradual.Models;

namespace Gradual.Services;

/// <summary>
/// Service for in-app notifications, due-date alerts, and overdue scanning (Features 51–60).
/// </summary>
public class NotificationService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    // Do Not Disturb mode (Feature 56)
    public bool DoNotDisturb { get; set; } = false;

    // Raised when a new notification is posted
    public event EventHandler<AppNotification>? NotificationPosted;

    public NotificationService(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
    }

    // ── Core ──────────────────────────────────────────────────────────────────

    public async Task<List<AppNotification>> LoadAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath)) return new();
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<AppNotification>>(json, _jsonOpts) ?? new();
        }
        finally { _lock.Release(); }
    }

    public async Task<AppNotification> PostAsync(string title, string message,
        NotificationLevel level = NotificationLevel.Info, string? projectId = null)
    {
        var notif = new AppNotification
        {
            Title = title,
            Message = message,
            Level = level,
            ProjectId = projectId
        };

        await _lock.WaitAsync();
        try
        {
            var all = await LoadInternalAsync();
            all.Add(notif);
            // Prune to last 200
            if (all.Count > 200) all = all.OrderByDescending(n => n.CreatedAt).Take(200).ToList();
            await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(all, _jsonOpts));
        }
        finally { _lock.Release(); }

        if (!DoNotDisturb || level == NotificationLevel.Critical)
            NotificationPosted?.Invoke(this, notif);

        return notif;
    }

    public async Task MarkReadAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadInternalAsync();
            var n = all.FirstOrDefault(x => x.Id == id);
            if (n != null) { n.IsRead = true; await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(all, _jsonOpts)); }
        }
        finally { _lock.Release(); }
    }

    public async Task MarkAllReadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadInternalAsync();
            all.ForEach(n => n.IsRead = true);
            await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(all, _jsonOpts));
        }
        finally { _lock.Release(); }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var all = await LoadAllAsync();
        return all.Count(n => !n.IsRead);
    }

    // ── Due-date scanning ─────────────────────────────────────────────────────

    /// <summary>
    /// Feature 51 / 52 / 59 — Scans projects and posts notifications for
    /// overdue projects and those due today or within warningDays.
    /// Returns a summary string for the daily digest.
    /// </summary>
    public async Task<string> ScanDueDatesAsync(IEnumerable<ProjectRecord> projects, int warningDays = 1)
    {
        var overdue = new List<ProjectRecord>();
        var dueToday = new List<ProjectRecord>();
        var dueSoon = new List<ProjectRecord>();

        foreach (var p in projects.Where(p => p.DueDate.HasValue && p.Status != "Completed"))
        {
            var days = p.DaysUntilDue;
            if (days < 0) overdue.Add(p);
            else if (days == 0) dueToday.Add(p);
            else if (days <= warningDays) dueSoon.Add(p);
        }

        if (overdue.Any())
            await PostAsync("⚠️ Overdue Projects",
                $"{overdue.Count} project(s) are past their due date: {string.Join(", ", overdue.Take(3).Select(p => p.ProjectName))}",
                NotificationLevel.Critical);

        if (dueToday.Any())
            await PostAsync("📅 Due Today",
                $"{dueToday.Count} project(s) due today: {string.Join(", ", dueToday.Take(3).Select(p => p.ProjectName))}",
                NotificationLevel.Warning);

        if (dueSoon.Any())
            await PostAsync("🔔 Due Soon",
                $"{dueSoon.Count} project(s) due within {warningDays} day(s)",
                NotificationLevel.Info);

        // Daily digest summary (Feature 59)
        return $"Today: {dueToday.Count} due, {overdue.Count} overdue, {projects.Count(p => p.Status == "Active")} active";
    }

    private async Task<List<AppNotification>> LoadInternalAsync()
    {
        if (!File.Exists(_filePath)) return new();
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<AppNotification>>(json, _jsonOpts) ?? new();
    }
}

/// <summary>
/// Notification model for in-app notification history (Feature 54).
/// </summary>
public class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationLevel Level { get; set; } = NotificationLevel.Info;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; } = false;
    public string? ProjectId { get; set; }
}

public enum NotificationLevel { Info, Warning, Critical }
