using System.Text.Json;
using Gradual.Models;

namespace Gradual.Services;

/// <summary>
/// Service for time-tracking operations (Features 41–50).
/// Persists entries to a JSON file alongside the projects file.
/// </summary>
public class TimeTrackingService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    // In-memory active timer (only one timer at a time)
    private TimeEntry? _activeTimer;

    public TimeTrackingService(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    public async Task<List<TimeEntry>> LoadAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath)) return new();
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<TimeEntry>>(json, _jsonOptions) ?? new();
        }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(TimeEntry entry)
    {
        await _lock.WaitAsync();
        try
        {
            var entries = await LoadInternalAsync();
            var existing = entries.FirstOrDefault(e => e.Id == entry.Id);
            if (existing == null) entries.Add(entry);
            else entries[entries.IndexOf(existing)] = entry;
            await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(entries, _jsonOptions));
        }
        finally { _lock.Release(); }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var entries = await LoadInternalAsync();
            await File.WriteAllTextAsync(_filePath,
                JsonSerializer.Serialize(entries.Where(e => e.Id != id).ToList(), _jsonOptions));
        }
        finally { _lock.Release(); }
    }

    // ── Timer ─────────────────────────────────────────────────────────────────

    /// <summary>Feature 41 — Start timer for a project.</summary>
    public TimeEntry StartTimer(Guid projectId, string projectName)
    {
        if (_activeTimer != null)
            StopTimer(); // auto-stop any running timer

        _activeTimer = new TimeEntry
        {
            ProjectId = projectId,
            ProjectName = projectName,
            TimerStartedAt = DateTime.Now,
            Date = DateTime.Today,
            IsBillable = true
        };
        return _activeTimer;
    }

    /// <summary>Feature 41 — Stop the active timer and persist the entry.</summary>
    public async Task<TimeEntry?> StopTimer()
    {
        if (_activeTimer == null) return null;

        var entry = _activeTimer;
        _activeTimer = null;

        var elapsed = DateTime.Now - entry.TimerStartedAt!.Value;
        entry.Hours = Math.Round((decimal)elapsed.TotalHours, 2);
        entry.TimerStartedAt = null; // clear running state

        await SaveAsync(entry);
        return entry;
    }

    public TimeEntry? GetActiveTimer() => _activeTimer;

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<TimeEntry>> GetByProjectAsync(Guid projectId)
    {
        var all = await LoadAllAsync();
        return all.Where(e => e.ProjectId == projectId).OrderByDescending(e => e.Date).ToList();
    }

    public async Task<Dictionary<Guid, decimal>> GetTodayHoursByProjectAsync()
    {
        var all = await LoadAllAsync();
        return all.Where(e => e.Date == DateTime.Today)
            .GroupBy(e => e.ProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Hours));
    }

    public async Task<decimal> GetTotalHoursForProjectAsync(Guid projectId)
    {
        var all = await LoadAllAsync();
        return all.Where(e => e.ProjectId == projectId).Sum(e => e.Hours);
    }

    /// <summary>Feature 45 — Returns hours per day for the last N days.</summary>
    public async Task<Dictionary<DateTime, decimal>> GetWeeklyReportAsync(int days = 7)
    {
        var cutoff = DateTime.Today.AddDays(-days + 1);
        var all = await LoadAllAsync();
        var result = new Dictionary<DateTime, decimal>();
        for (int i = 0; i < days; i++)
            result[DateTime.Today.AddDays(-i)] = 0;
        foreach (var g in all.Where(e => e.Date >= cutoff).GroupBy(e => e.Date))
            result[g.Key] = g.Sum(e => e.Hours);
        return result;
    }

    /// <summary>Feature 48 — Export time entries to CSV.</summary>
    public async Task ExportToCsvAsync(IEnumerable<TimeEntry> entries, string filePath)
    {
        var lines = new List<string> { "Date,Project,Hours,Billable,Description" };
        foreach (var e in entries)
            lines.Add($"{e.Date:yyyy-MM-dd},{CsvQ(e.ProjectName)},{e.Hours:F2},{e.IsBillable},{CsvQ(e.Description)}");
        await File.WriteAllLinesAsync(filePath, lines, System.Text.Encoding.UTF8);
    }

    private static string CsvQ(string? s) =>
        s == null ? "" : (s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s);

    private async Task<List<TimeEntry>> LoadInternalAsync()
    {
        if (!File.Exists(_filePath)) return new();
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<TimeEntry>>(json, _jsonOptions) ?? new();
    }
}
