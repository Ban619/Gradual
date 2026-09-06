using Gradual.Infrastructure;
using Gradual.Models;

namespace Gradual.Forms;

/// <summary>
/// Audit Log Viewer — shows the recorded audit trail (Feature 95).
/// </summary>
public class AuditLog_Viewer : Form
{
    private ListView _list = null!;
    private TextBox _searchBox = null!;
    private Label _countLabel = null!;

    private static readonly Color Bg   = Color.FromArgb(13, 17, 23);
    private static readonly Color Card = Color.FromArgb(22, 27, 34);
    private static readonly Color TextClr = Color.FromArgb(201, 209, 217);
    private static readonly Color Muted= Color.FromArgb(139, 148, 158);
    private static readonly Color Accent= Color.FromArgb(88, 166, 255);

    // The audit log file path (written by AuditTrailManager via Serilog)
    private readonly string _auditLogDir;
    private List<AuditEntry> _allEntries = new();

    public AuditLog_Viewer(string auditLogDir)
    {
        _auditLogDir = auditLogDir;
        BuildUI();
        LoadEntries();
    }

    private void BuildUI()
    {
        Text = "🔍 Audit Log Viewer";
        Size = new Size(980, 640);
        BackColor = Bg;
        ForeColor = TextClr;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Card,
            Padding = new Padding(12, 8, 12, 8)
        };

        var titleLbl = new Label
        {
            Text = "🔍 Audit Log",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Accent,
            AutoSize = true,
            Location = new Point(12, 12)
        };

        _searchBox = new TextBox
        {
            PlaceholderText = "Search audit log...",
            Width = 260,
            Height = 28,
            Location = new Point(200, 13),
            BackColor = Color.FromArgb(30, 37, 44),
            ForeColor = TextClr,
            BorderStyle = BorderStyle.FixedSingle
        };
        _searchBox.TextChanged += (s, e) => ApplyFilter();

        _countLabel = new Label
        {
            Text = "0 entries",
            ForeColor = Muted,
            AutoSize = true,
            Location = new Point(480, 18)
        };

        var exportBtn = new Button
        {
            Text = "⬇ Export CSV",
            Height = 28,
            AutoSize = true,
            BackColor = Color.FromArgb(36, 96, 176),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(860, 12),
            Cursor = Cursors.Hand
        };
        exportBtn.FlatAppearance.BorderSize = 0;
        exportBtn.Click += ExportCsv_Click;

        toolbar.Controls.Add(titleLbl);
        toolbar.Controls.Add(_searchBox);
        toolbar.Controls.Add(_countLabel);
        toolbar.Controls.Add(exportBtn);

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            BackColor = Card,
            ForeColor = TextClr,
            GridLines = false,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9)
        };
        _list.Columns.Add("Timestamp", 160);
        _list.Columns.Add("Action", 90);
        _list.Columns.Add("Entity", 80);
        _list.Columns.Add("Entity ID", 120);
        _list.Columns.Add("User", 90);
        _list.Columns.Add("Details", 360);

        Controls.Add(_list);
        Controls.Add(toolbar);
    }

    private void LoadEntries()
    {
        _allEntries.Clear();

        // Try reading from Serilog log files in the logs directory
        if (!Directory.Exists(_auditLogDir))
        {
            AddSampleEntry("No audit log directory found at: " + _auditLogDir);
            return;
        }

        var logFiles = Directory.GetFiles(_auditLogDir, "*.log")
            .OrderByDescending(f => f).Take(5).ToList();

        if (!logFiles.Any())
        {
            // Also check for JSON-format audit files
            logFiles = Directory.GetFiles(_auditLogDir, "*.json")
                .OrderByDescending(f => f).Take(5).ToList();
        }

        foreach (var logFile in logFiles)
        {
            try
            {
                var lines = File.ReadLines(logFile).Take(2000);
                foreach (var line in lines)
                {
                    // Simple: look for lines containing known audit keywords
                    if (line.Contains("Audit") || line.Contains("[INF]") || line.Contains("[WRN]"))
                    {
                        _allEntries.Add(new AuditEntry
                        {
                            Timestamp = ExtractTimestamp(line),
                            Action = ExtractAction(line),
                            Entity = "Project",
                            EntityId = "",
                            User = ExtractUser(line),
                            Details = line.Length > 200 ? line[..200] + "…" : line
                        });
                    }
                }
            }
            catch { /* skip unreadable files */ }
        }

        if (!_allEntries.Any())
            AddSampleEntry("Audit log is empty — perform operations to see entries here.");

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var search = _searchBox.Text.ToLowerInvariant();
        var filtered = string.IsNullOrWhiteSpace(search)
            ? _allEntries
            : _allEntries.Where(e =>
                e.Details.ContainsI(search) ||
                e.Action.ContainsI(search) ||
                e.User.ContainsI(search)).ToList();

        _list.Items.Clear();
        foreach (var entry in filtered.Take(500))
        {
            var item = new ListViewItem(entry.Timestamp);
            item.SubItems.Add(entry.Action);
            item.SubItems.Add(entry.Entity);
            item.SubItems.Add(entry.EntityId);
            item.SubItems.Add(entry.User);
            item.SubItems.Add(entry.Details);
            _list.Items.Add(item);
        }
        _countLabel.Text = $"{filtered.Count} entries";
    }

    private void AddSampleEntry(string details)
    {
        _allEntries.Add(new AuditEntry
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Action = "INFO",
            Entity = "System",
            EntityId = "",
            User = Environment.UserName,
            Details = details
        });
    }

    private async void ExportCsv_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "audit_log.csv" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var lines = new List<string> { "Timestamp,Action,Entity,EntityId,User,Details" };
        foreach (ListViewItem item in _list.Items)
            lines.Add(string.Join(",", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => $"\"{s.Text.Replace("\"", "\"\"")}\""))); 

        await File.WriteAllLinesAsync(dlg.FileName, lines);
        MessageBox.Show("Exported.", "Audit Log");
    }

    private static string ExtractTimestamp(string line)
    {
        if (line.Length >= 23 && DateTime.TryParse(line[..23], out var dt))
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string ExtractAction(string line)
    {
        if (line.Contains("[ERR]")) return "ERROR";
        if (line.Contains("[WRN]")) return "WARN";
        if (line.Contains("[INF]")) return "INFO";
        if (line.Contains("Created")) return "CREATE";
        if (line.Contains("Updated") || line.Contains("Saved")) return "UPDATE";
        if (line.Contains("Deleted")) return "DELETE";
        return "INFO";
    }

    private static string ExtractUser(string line)
    {
        var idx = line.IndexOf("User=", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var rest = line[(idx + 5)..];
            return rest.Split([' ', ',', ']'], 2)[0];
        }
        return Environment.UserName;
    }

    private record AuditEntry
    {
        public string Timestamp { get; init; } = "";
        public string Action { get; init; } = "";
        public string Entity { get; init; } = "";
        public string EntityId { get; init; } = "";
        public string User { get; init; } = "";
        public string Details { get; init; } = "";
    }
}
