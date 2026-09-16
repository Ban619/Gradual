namespace Gradual.Forms;

/// <summary>
/// What's New dialog — shown once on upgrade via version tracking (Feature 100).
/// </summary>
public class WhatsNewDialog : Form
{
    private static readonly Color Bg    = Color.FromArgb(13, 17, 23);
    private static readonly Color Card  = Color.FromArgb(22, 27, 34);
    private static readonly Color TextClr = Color.FromArgb(201, 209, 217);
    private static readonly Color Accent= Color.FromArgb(88, 166, 255);
    private static readonly Color Green = Color.FromArgb(63, 185, 80);
    private static readonly Color Muted = Color.FromArgb(139, 148, 158);

    private const string CurrentVersion = "2.0.0";
    private const string VersionKey = "LastSeenVersion";

    public WhatsNewDialog()
    {
        BuildUI();
    }

    /// <summary>Show the dialog once per version upgrade.</summary>
    private static readonly string _versionFile = Path.Combine(
        AppContext.BaseDirectory, ".last_seen_version");

    public static void ShowIfNew()
    {
        var lastSeen = File.Exists(_versionFile) ? File.ReadAllText(_versionFile).Trim() : "";
        if (lastSeen == CurrentVersion) return;

        using var dlg = new WhatsNewDialog();
        dlg.ShowDialog();

        File.WriteAllText(_versionFile, CurrentVersion);
    }

    /// <summary>Show the dialog unconditionally (e.g. from Help menu).</summary>
    public static void ShowAlways()
    {
        using var dlg = new WhatsNewDialog();
        dlg.ShowDialog();
    }

    private void BuildUI()
    {
        Text = $"🎉 What's New in Gradual v{CurrentVersion}";
        Size = new Size(680, 640);
        BackColor = Bg;
        ForeColor = TextClr;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // hero
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // changelog
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // footer

        // ── Hero ──────────────────────────────────────────────────────────────
        var hero = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 35, 65) };
        hero.Paint += (s, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                hero.ClientRectangle,
                Color.FromArgb(18, 35, 65),
                Color.FromArgb(10, 22, 42),
                System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, hero.ClientRectangle);
        };

        var versionBadge = new Label
        {
            Text = $"v{CurrentVersion}",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Accent,
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Location = new Point(24, 20)
        };

        var heroTitle = new Label
        {
            Text = "🎉 100 New Features!",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 52)
        };

        var subTitle = new Label
        {
            Text = "Gradual has been supercharged with time tracking, notifications, advanced analytics & more.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(160, 185, 220),
            AutoSize = false,
            Size = new Size(620, 22),
            Location = new Point(24, 80)
        };

        hero.Controls.Add(versionBadge);
        hero.Controls.Add(heroTitle);
        hero.Controls.Add(subTitle);
        layout.Controls.Add(hero, 0, 0);

        // ── Changelog ─────────────────────────────────────────────────────────
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg, Padding = new Padding(20, 12, 20, 0) };

        var changePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Width = 618,
            BackColor = Bg
        };

        var groups = new[]
        {
            ("🗃 Model Enrichment (Group A)",  new[] {
                "✦ Due dates with overdue detection",
                "✦ Estimated hours & completion percentage slider",
                "✦ Tag/label system with multi-tag filtering",
                "✦ Per-project color coding",
                "✦ Budget tracking with currency support",
                "✦ External URL linking",
                "✦ Project templates (create from template)",
                "✦ Star/favourite flag",
                "✦ Recurrence rules (daily/weekly/monthly/yearly)"
            }),
            ("♻ Lifecycle & CRUD (Group B)", new[] {
                "✦ Archive projects",
                "✦ Clone / duplicate project",
                "✦ Bulk status change",
                "✦ Soft delete with Recycle Bin",
                "✦ Restore from Recycle Bin",
                "✦ Status change history with reason",
                "✦ Undo last delete (Ctrl+Z)",
                "✦ Completion % auto-completes project at 100%"
            }),
            ("🔍 Search & Filtering (Group C)", new[] {
                "✦ Saved searches",
                "✦ Date range filter (created or due date)",
                "✦ Tag filter chips",
                "✦ Client filter combo",
                "✦ Filter presets (Overdue, Due Today, Starred, etc.)",
                "✦ Advanced filter dialog",
                "✦ Full-text notes search with highlight",
                "✦ Global quick-search Ctrl+F"
            }),
            ("📤 Import / Export (Group D)", new[] {
                "✦ Export to CSV (with all new fields)",
                "✦ Export to JSON",
                "✦ Export to PDF",
                "✦ Import from CSV",
                "✦ Import from JSON",
                "✦ Import from Excel (.xlsx)",
                "✦ Clipboard paste import",
                "✦ Print project list"
            }),
            ("⏱ Time Tracking (Group E)", new[] {
                "✦ Live start/stop timer",
                "✦ Manual time entry",
                "✦ Today's time log panel",
                "✦ Daily & weekly reports",
                "✦ Billable / non-billable flag",
                "✦ Time vs estimate progress bar",
                "✦ Idle detection warning",
                "✦ Time export to CSV/Excel"
            }),
            ("🔔 Notifications (Group F)", new[] {
                "✦ Due-date system-tray reminders",
                "✦ Overdue alert banner",
                "✦ Notification Center panel",
                "✦ Read/unread tracking & history",
                "✦ Sound alerts",
                "✦ Do Not Disturb mode",
                "✦ Daily digest on startup"
            }),
            ("🎨 UI & UX (Group G)", new[] {
                "✦ Full light/dark theme toggle",
                "✦ Compact layout mode",
                "✦ F1 keyboard shortcut overlay",
                "✦ Column visibility toggles",
                "✦ Status-bar summary footer",
                "✦ Zoom / font size (Ctrl+Wheel)",
                "✦ Tooltips on all list items"
            }),
            ("📈 Analytics (Group H)", new[] {
                "✦ Client leaderboard",
                "✦ Monthly creation trend chart",
                "✦ Burndown chart",
                "✦ Phase heatmap",
                "✦ Productivity score",
                "✦ Export dashboard as PNG image"
            }),
            ("🐙 GitHub Integration (Group I)", new[] {
                "✦ Repo ↔ Project linking",
                "✦ Commit & issue count badges",
                "✦ Pull request sub-panel",
                "✦ GitHub Actions status badge",
                "✦ Star / watch repo from dashboard",
                "✦ Create GitHub issue from project"
            }),
            ("⚙ Quality & Maintenance (Group J)", new[] {
                "✦ Backup on demand (ZIP)",
                "✦ Restore from backup",
                "✦ Data integrity check",
                "✦ System health dashboard",
                "✦ Audit log viewer",
                "✦ Settings form UI",
                "✦ Recurring project auto-cloning"
            })
        };

        foreach (var (groupTitle, items) in groups)
        {
            var groupLbl = new Label
            {
                Text = groupTitle,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Accent,
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 4)
            };
            changePanel.Controls.Add(groupLbl);

            foreach (var item in items)
            {
                var itemLbl = new Label
                {
                    Text = item,
                    Font = new Font("Segoe UI", 9),
                    ForeColor = TextClr,
                    AutoSize = true,
                    Margin = new Padding(16, 1, 0, 1)
                };
                changePanel.Controls.Add(itemLbl);
            }
        }

        scroll.Controls.Add(changePanel);
        layout.Controls.Add(scroll, 0, 1);

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            Padding = new Padding(16, 8, 16, 8)
        };
        footer.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(48, 54, 61), 1);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        var closeBtn = new Button
        {
            Text = "🚀 Get Started",
            Width = 160,
            Height = 36,
            Dock = DockStyle.Right,
            BackColor = Accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        closeBtn.FlatAppearance.BorderSize = 0;
        closeBtn.Click += (s, e) => Close();
        footer.Controls.Add(closeBtn);

        layout.Controls.Add(footer, 0, 2);

        Controls.Add(layout);
    }
}
