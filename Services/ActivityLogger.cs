using System.Text.Json;
using Gradual.Models;

namespace Gradual.Services;

/// <summary>
/// Persists and retrieves ActivityEntry records that track every project change.
/// Stored as a JSON array in the same data directory as other app data.
/// </summary>
public class ActivityLogger
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ActivityLogger(ConfigurationService configService)
    {
        var dataDir = Path.Combine(
            AppContext.BaseDirectory,
            configService.Settings.ApplicationSettings.DataDirectory);
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "activity_log.json");
    }

    /// <summary>Appends a new activity entry to the log.</summary>
    public async Task LogAsync(ActivityEntry entry)
    {
        await _lock.WaitAsync();
        try
        {
            var entries = await ReadAllAsync();
            entries.Insert(0, entry); // newest first
            await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(entries, _jsonOptions));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Returns the most recent <paramref name="count"/> entries (newest first).</summary>
    public async Task<List<ActivityEntry>> GetRecentAsync(int count = 200)
    {
        var all = await ReadAllAsync();
        return all.Take(count).ToList();
    }

    private async Task<List<ActivityEntry>> ReadAllAsync()
    {
        if (!File.Exists(_filePath))
            return new List<ActivityEntry>();
        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<ActivityEntry>>(json) ?? new();
        }
        catch
        {
            return new List<ActivityEntry>();
        }
    }
}
