using Gradual.Infrastructure;

namespace Gradual.Forms;

/// <summary>
/// System Health Dashboard — disk, memory, data stats (Feature 94).
/// </summary>
public class SystemHealthForm : Form
{
    private static readonly Color Bg    = Color.FromArgb(13, 17, 23);
    private static readonly Color Card  = Color.FromArgb(22, 27, 34);
    private static readonly Color TextClr = Color.FromArgb(201, 209, 217);
    private static readonly Color Green = Color.FromArgb(63, 185, 80);
    private static readonly Color Warn  = Color.FromArgb(210, 153, 34);
    private static readonly Color Red   = Color.FromArgb(248, 81, 73);
    private static readonly Color Accent= Color.FromArgb(88, 166, 255);
    private static readonly Color Muted = Color.FromArgb(139, 148, 158);

    private FlowLayoutPanel _metricsPanel = null!;
    private System.Windows.Forms.Timer _refreshTimer = null!;

    public SystemHealthForm()
    {
        BuildUI();
        Refresh();
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _refreshTimer.Tick += (s, e) => Refresh();
        _refreshTimer.Start();
    }

    private void BuildUI()
    {
        Text = "⚡ System Health";
        Size = new Size(760, 560);
        BackColor = Bg;
        ForeColor = TextClr;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Card,
            Padding = new Padding(16, 12, 16, 12)
        };
        var titleLbl = new Label
        {
            Text = "⚡ System Health",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Green,
            AutoSize = true,
            Location = new Point(16, 12)
        };

        var refreshBtn = new Button
        {
            Text = "🔄 Refresh",
            Location = new Point(650, 12),
            Height = 28,
            AutoSize = true,
            BackColor = Color.FromArgb(30, 37, 44),
            ForeColor = TextClr,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        refreshBtn.FlatAppearance.BorderColor = Color.FromArgb(48, 54, 61);
        refreshBtn.Click += (s, e) => Refresh();

        header.Controls.Add(titleLbl);
        header.Controls.Add(refreshBtn);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg };

        _metricsPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            BackColor = Bg,
            Padding = new Padding(14),
            Width = 720
        };

        scroll.Controls.Add(_metricsPanel);
        Controls.Add(scroll);
        Controls.Add(header);
    }

    private new void Refresh()
    {
        _metricsPanel.Controls.Clear();

        // ── Application metrics ─────────────────────────────────────────────
        var proc = System.Diagnostics.Process.GetCurrentProcess();
        var memMb = proc.WorkingSet64 / 1024 / 1024;
        var memColor = memMb < 200 ? Green : memMb < 500 ? Warn : Red;
        AddMetricCard("💾 Memory Usage", $"{memMb} MB", memColor,
            memMb < 200 ? "Healthy" : memMb < 500 ? "Moderate" : "High");

        var uptime = DateTime.Now - proc.StartTime;
        AddMetricCard("⏱ Uptime", $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s", Accent, "Running");

        AddMetricCard("🧵 Threads", proc.Threads.Count.ToString(), Accent, "Active threads");
        AddMetricCard("📊 GC Gen0 Coll.", GC.CollectionCount(0).ToString(), Muted, "Collections");
        AddMetricCard("📊 GC Gen1 Coll.", GC.CollectionCount(1).ToString(), Muted, "Collections");
        AddMetricCard("📊 GC Gen2 Coll.", GC.CollectionCount(2).ToString(), Muted, "Collections");

        // ── Disk metrics ─────────────────────────────────────────────────────
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\");
            var freeMb = drive.AvailableFreeSpace / 1024 / 1024;
            var totalMb = drive.TotalSize / 1024 / 1024;
            var usedPct = 100 - (int)(100.0 * drive.AvailableFreeSpace / drive.TotalSize);
            var diskColor = usedPct < 75 ? Green : usedPct < 90 ? Warn : Red;
            AddMetricCard("💽 Disk Free", $"{freeMb / 1024:F1} GB", diskColor, $"{usedPct}% used of {totalMb / 1024:F0} GB");
        }
        catch { AddMetricCard("💽 Disk", "N/A", Muted, "Unable to read"); }

        // ── Data file metrics ─────────────────────────────────────────────────
        try
        {
            var config = ServiceContainer.GetRequiredService<Services.ConfigurationService>();
            var dataDir = Path.Combine(AppContext.BaseDirectory, config.Settings.ApplicationSettings.DataDirectory);
            if (Directory.Exists(dataDir))
            {
                var dataFiles = Directory.GetFiles(dataDir, "*.*", SearchOption.AllDirectories);
                var totalSize = dataFiles.Sum(f => new FileInfo(f).Length) / 1024;
                AddMetricCard("📁 Data Files", $"{dataFiles.Length} files", Accent, $"{totalSize} KB total");

                var projFile = Path.Combine(dataDir, config.Settings.ApplicationSettings.ProjectsFileName);
                if (File.Exists(projFile))
                {
                    var projSizeKb = new FileInfo(projFile).Length / 1024;
                    AddMetricCard("📋 projects.json", $"{projSizeKb} KB", Accent, "Data file");
                }

                var backupDir = Path.Combine(AppContext.BaseDirectory, config.Settings.ApplicationSettings.BackupDirectory);
                var backupCount = Directory.Exists(backupDir)
                    ? Directory.GetFiles(backupDir, "*.zip").Length : 0;
                AddMetricCard("🔒 Backups", backupCount.ToString(), backupCount > 0 ? Green : Warn, "ZIP backups stored");
            }
        }
        catch { }

        // ── .NET Runtime ─────────────────────────────────────────────────────
        AddMetricCard("🔷 .NET Runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, Accent, "Runtime version");
        AddMetricCard("💻 OS", System.Runtime.InteropServices.RuntimeInformation.OSDescription.Truncate(40), Accent, "Operating system");
        AddMetricCard("🕐 Local Time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Muted, "System clock");
    }

    private void AddMetricCard(string title, string value, Color valueColor, string subtitle)
    {
        var card = new Panel
        {
            Width = 200,
            Height = 86,
            BackColor = Card,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 10, 10),
            Cursor = Cursors.Default
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(48, 54, 61), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var titleLbl = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Muted,
            AutoSize = true,
            Location = new Point(12, 10)
        };

        var valueLbl = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = valueColor,
            AutoSize = true,
            Location = new Point(12, 28),
            AutoEllipsis = true,
            MaximumSize = new Size(176, 0)
        };

        var subtitleLbl = new Label
        {
            Text = subtitle,
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = Color.FromArgb(80, 90, 110),
            AutoSize = true,
            Location = new Point(12, 64)
        };

        card.Controls.Add(titleLbl);
        card.Controls.Add(valueLbl);
        card.Controls.Add(subtitleLbl);
        _metricsPanel.Controls.Add(card);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        base.OnFormClosing(e);
    }
}
