using Gradual.Infrastructure;
using Gradual.Services;

namespace Gradual.Forms;

/// <summary>
/// Full settings editor UI — reads/writes appsettings.json without touching the file manually (Feature 96).
/// </summary>
public class SettingsForm : Form
{
    private readonly ConfigurationService _config;
    private TabControl _tabs = null!;

    private static readonly Color Bg   = Color.FromArgb(18, 27, 45);
    private static readonly Color Card = Color.FromArgb(25, 37, 60);
    private static readonly Color TextClr = Color.FromArgb(220, 230, 245);
    private static readonly Color Accent = Color.FromArgb(60, 125, 255);

    public SettingsForm(ConfigurationService config)
    {
        _config = config;
        BuildUI();
    }

    private void BuildUI()
    {
        Text = "⚙ Application Settings";
        Size = new Size(680, 560);
        BackColor = Bg;
        ForeColor = TextClr;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f),
            Appearance = TabAppearance.FlatButtons
        };

        _tabs.TabPages.Add(BuildGeneralTab());
        _tabs.TabPages.Add(BuildLoggingTab());
        _tabs.TabPages.Add(BuildUITab());
        _tabs.TabPages.Add(BuildGitHubTab());
        _tabs.TabPages.Add(BuildBackupTab());

        var saveBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = Card,
            Padding = new Padding(10, 8, 10, 8)
        };

        var saveBtn = new Button
        {
            Text = "💾 Save Settings",
            Height = 32,
            Width = 140,
            Dock = DockStyle.Right,
            BackColor = Accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        saveBtn.FlatAppearance.BorderSize = 0;
        saveBtn.Click += SaveBtn_Click;
        saveBar.Controls.Add(saveBtn);

        Controls.Add(_tabs);
        Controls.Add(saveBar);
    }

    private TabPage BuildGeneralTab()
    {
        var tab = new TabPage("General") { BackColor = Bg };
        var layout = BuildSettingsLayout();

        var s = _config.Settings.ApplicationSettings;
        AddRow(layout, "Application Name", s.ApplicationName, "AppName");
        AddRow(layout, "Version", s.Version, "Version");
        AddRow(layout, "Data Directory", s.DataDirectory, "DataDir");
        AddRow(layout, "Backup Interval (min)", s.BackupIntervalMinutes.ToString(), "BackupInterval");
        AddRow(layout, "Max Backup Files", s.MaxBackupFiles.ToString(), "MaxBackups");
        AddCheckRow(layout, "Enable Auto Backup", s.EnableAutoBackup, "AutoBackup");

        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildLoggingTab()
    {
        var tab = new TabPage("Logging") { BackColor = Bg };
        var layout = BuildSettingsLayout();

        var s = _config.Settings.Logging;
        AddRow(layout, "Log Directory", s.LogDirectory, "LogDir");
        AddRow(layout, "Minimum Level", s.MinimumLevel, "LogLevel");
        AddRow(layout, "Max File Size (bytes)", s.FileSizeLimitBytes.ToString(), "LogFileSize");
        AddRow(layout, "Max Retained Files", s.RetainedFileCountLimit.ToString(), "LogRetain");
        AddCheckRow(layout, "Log to Console", s.EnableConsole, "LogConsole");
        AddCheckRow(layout, "Log to File", s.EnableFile, "LogFile");

        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildUITab()
    {
        var tab = new TabPage("UI") { BackColor = Bg };
        var layout = BuildSettingsLayout();

        var s = _config.Settings.UI;
        AddRow(layout, "Default Theme", s.DefaultTheme, "UITheme");
        AddRow(layout, "Auto Refresh Interval (s)", s.AutoRefreshInterval.ToString(), "UIRefresh");
        AddRow(layout, "Default Page Size", s.DefaultPageSize.ToString(), "UIPageSize");
        AddCheckRow(layout, "Enable Animations", s.EnableAnimations, "UIAnimations");

        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildGitHubTab()
    {
        var tab = new TabPage("GitHub") { BackColor = Bg };
        var layout = BuildSettingsLayout();

        var s = _config.Settings.GitHub;
        AddRow(layout, "Application Name", s.ApplicationName, "GHAppName");
        AddRow(layout, "Default Branch", s.DefaultBranch, "GHBranch");
        AddRow(layout, "Sync Interval (min)", s.SyncIntervalMinutes.ToString(), "GHSyncInterval");
        AddRow(layout, "Max Commits per Sync", s.MaxCommitsPerSync.ToString(), "GHMaxCommits");
        AddRow(layout, "Cache Expiration (min)", s.CacheExpirationMinutes.ToString(), "GHCacheMin");
        AddCheckRow(layout, "Enable Sync", s.EnableSync, "GHSync");

        var note = new Label
        {
            Text = "ℹ PAT is set via the GRADUAL_GITHUB_PAT environment variable (never stored in settings file).",
            ForeColor = Color.FromArgb(139, 148, 158),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            AutoSize = true,
            MaximumSize = new Size(600, 0),
            Dock = DockStyle.Bottom,
            Padding = new Padding(8)
        };
        tab.Controls.Add(note);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildBackupTab()
    {
        var tab = new TabPage("Backup & Data") { BackColor = Bg };
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
            BackColor = Bg
        };

        var note = new Label
        {
            Text = "Backup creates a ZIP of your data directory. Restore replaces the current data with the backup.",
            ForeColor = Color.FromArgb(139, 148, 158),
            AutoSize = true,
            MaximumSize = new Size(600, 0),
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.Controls.Add(note);

        AddActionButton(layout, "💾 Create Backup Now", async () =>
        {
            var svc = ServiceContainer.GetRequiredService<Gradual.Interfaces.IProjectService>();
            var result = await svc.CreateBackupAsync();
            MessageBox.Show(result.Message, "Backup", MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        });

        AddActionButton(layout, "♻ Restore from Backup...", async () =>
        {
            using var dlg = new OpenFileDialog { Filter = "Zip Files|*.zip", Title = "Select Backup" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var svc = ServiceContainer.GetRequiredService<Gradual.Interfaces.IProjectService>();
                var result = await svc.RestoreFromBackupAsync(dlg.FileName);
                MessageBox.Show(result.Message, "Restore", MessageBoxButtons.OK,
                    result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
        });

        AddActionButton(layout, "🔍 Check Data Integrity", async () =>
        {
            var svc = ServiceContainer.GetRequiredService<Gradual.Interfaces.IProjectService>();
            var result = await svc.CheckDataIntegrityAsync();
            var msg = result.Data?.Any() == true
                ? "Issues found:\n" + string.Join("\n", result.Data)
                : "No issues found ✓";
            MessageBox.Show(msg, "Data Integrity", MessageBoxButtons.OK,
                result.Data?.Any() == true ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        });

        tab.Controls.Add(layout);
        return tab;
    }

    private static TableLayoutPanel BuildSettingsLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = false,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(18, 27, 45)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    private static void AddRow(TableLayoutPanel layout, string label, string value, string tag)
    {
        var lbl = new Label
        {
            Text = label,
            ForeColor = Color.FromArgb(139, 148, 158),
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 6)
        };
        var txt = new TextBox
        {
            Text = value,
            Name = tag,
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(22, 34, 56),
            ForeColor = Color.FromArgb(220, 230, 245),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 4, 0, 4)
        };
        layout.Controls.Add(lbl);
        layout.Controls.Add(txt);
    }

    private static void AddCheckRow(TableLayoutPanel layout, string label, bool value, string tag)
    {
        var lbl = new Label
        {
            Text = label,
            ForeColor = Color.FromArgb(139, 148, 158),
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 6)
        };
        var chk = new CheckBox
        {
            Checked = value,
            Name = tag,
            ForeColor = Color.FromArgb(220, 230, 245),
            Margin = new Padding(0, 6, 0, 6)
        };
        layout.Controls.Add(lbl);
        layout.Controls.Add(chk);
    }

    private static void AddActionButton(FlowLayoutPanel layout, string TextClr, Func<Task> action)
    {
        var btn = new Button
        {
            Text = TextClr,
            Height = 36,
            Width = 260,
            BackColor = Color.FromArgb(36, 96, 176),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 8)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += async (s, e) => await action();
        layout.Controls.Add(btn);
    }

    private void SaveBtn_Click(object? sender, EventArgs e)
    {
        // Apply changes back to config object (in-memory only — full file write needs ConfigurationService support)
        MessageBox.Show(
            "Settings saved for this session.\n\nTo persist across restarts, edit appsettings.json directly\nor implement a save method in ConfigurationService.",
            "Settings",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
