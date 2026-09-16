using System.Drawing.Drawing2D;
using Gradual.Infrastructure;
using Gradual.Models;
using Gradual.Models.GitHub;
using Gradual.Services;
using Gradual.Services.GitHub;

namespace Gradual.Forms;

/// <summary>
/// Board panel: two tabs — Projects (Kanban) and GitHub Repositories.
/// </summary>
public class BoardPanel : UserControl
{
    // ── palette ────────────────────────────────────────────────────────────
    private static readonly Color BgPage      = Color.FromArgb(10,  18,  34);
    private static readonly Color BgColumn    = Color.FromArgb(16,  26,  46);
    private static readonly Color BgCard      = Color.FromArgb(22,  36,  60);
    private static readonly Color BgFeed      = Color.FromArgb(14,  22,  40);
    private static readonly Color BgRepoCard  = Color.FromArgb(18,  30,  52);
    private static readonly Color Accent      = Color.FromArgb(94, 129, 255);
    private static readonly Color AccentGreen = Color.FromArgb(49, 196, 141);
    private static readonly Color AccentAmber = Color.FromArgb(247, 173,  64);
    private static readonly Color ColActive   = Color.FromArgb(49, 196, 141);
    private static readonly Color ColOnHold   = Color.FromArgb(247, 173,  64);
    private static readonly Color ColCompleted= Color.FromArgb(94, 129, 255);
    private static readonly Color ColTodo     = Color.FromArgb(160, 185, 220);
    private static readonly Color TextPrimary = Color.FromArgb(220, 233, 255);
    private static readonly Color TextMuted   = Color.FromArgb(120, 150, 190);
    private static readonly Color Border      = Color.FromArgb(34,  54,  82);

    private static readonly Dictionary<string, Color> PriorityColors = new()
    {
        ["High"]   = Color.FromArgb(220, 60,  80),
        ["Normal"] = Color.FromArgb(60, 120, 220),
        ["Low"]    = Color.FromArgb(60, 160, 100),
    };

    private static readonly Dictionary<string, (string icon, Color color)> ActionMeta = new()
    {
        ["Created"]       = ("✦", Color.FromArgb(49, 196, 141)),
        ["Updated"]       = ("✎", Color.FromArgb(94, 129, 255)),
        ["Deleted"]       = ("✕", Color.FromArgb(220, 70,  70)),
        ["StatusChanged"] = ("⇄", Color.FromArgb(247, 173,  64)),
    };

    // ── Kanban columns + feed ──────────────────────────────────────────────
    private Panel _colTodo      = null!;
    private Panel _colActive    = null!;
    private Panel _colOnHold    = null!;
    private Panel _colCompleted = null!;
    private Panel _feedPanel    = null!;

    // ── tab panels ─────────────────────────────────────────────────────────
    private Panel  _projectsTab   = null!;
    private Panel  _reposTab      = null!;
    private Button _btnProjects   = null!;
    private Button _btnRepos      = null!;
    private Label  _repoStatusLbl = null!;
    private FlowLayoutPanel _repoFlow = null!;

    private ActivityLogger? _logger;
    // Cancellation for in-flight GitHub repo fetch (cancelled when user switches away)
    private CancellationTokenSource? _repoCts;

    public BoardPanel()
    {
        BackColor = BgPage;
        Dock      = DockStyle.Fill;
        BuildLayout();
    }

    // ── public API ─────────────────────────────────────────────────────────

    public async Task RefreshAsync(IEnumerable<ProjectRecord> projects, ActivityLogger logger)
    {
        _logger = logger;
        var list = projects.ToList();

        PopulateColumn(_colTodo,      "TO DO",     ColTodo,      list.Where(p => p.Status == "To Do").ToList());
        PopulateColumn(_colActive,    "ACTIVE",    ColActive,    list.Where(p => p.Status == "Active").ToList());
        PopulateColumn(_colOnHold,    "ON HOLD",   ColOnHold,    list.Where(p => p.Status == "On Hold").ToList());
        PopulateColumn(_colCompleted, "COMPLETED", ColCompleted, list.Where(p => p.Status == "Completed").ToList());

        var entries = await logger.GetRecentAsync(100);
        PopulateFeed(entries);
    }

    // ── layout ────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        // ── header ───────────────────────────────────────────────────────
        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 52,
            BackColor = Color.FromArgb(8, 16, 30),
            Padding   = new Padding(20, 0, 20, 0)
        };
        header.Controls.Add(new Label
        {
            Text      = "Board",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location  = new Point(20, 10)
        });
        Controls.Add(header);

        // ── tab bar ───────────────────────────────────────────────────────
        var tabBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 42,
            BackColor = Color.FromArgb(12, 20, 38)
        };

        _btnProjects = MakeTabBtn("📋  Projects");
        _btnRepos    = MakeTabBtn("🐙  GitHub Repositories");
        _btnProjects.Location = new Point(0, 0);
        _btnRepos.Location    = new Point(_btnProjects.Width, 0);
        _btnProjects.Click += (_, _) => SwitchTab(projects: true);
        _btnRepos.Click    += (_, _) => { SwitchTab(projects: false); _ = LoadGitHubReposAsync(); };
        tabBar.Controls.Add(_btnProjects);
        tabBar.Controls.Add(_btnRepos);
        Controls.Add(tabBar);

        // ── Projects tab ──────────────────────────────────────────────────
        _projectsTab = new Panel { Dock = DockStyle.Fill, BackColor = BgPage };

        var outerSplit = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 1,
            BackColor   = BgPage
        };
        outerSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
        outerSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
        outerSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var kanban = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 4,
            RowCount    = 1,
            BackColor   = BgPage,
            Padding     = new Padding(12, 12, 6, 12)
        };
        for (int i = 0; i < 4; i++)
            kanban.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        kanban.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _colTodo      = BuildColumn();
        _colActive    = BuildColumn();
        _colOnHold    = BuildColumn();
        _colCompleted = BuildColumn();

        kanban.Controls.Add(_colTodo,      0, 0);
        kanban.Controls.Add(_colActive,    1, 0);
        kanban.Controls.Add(_colOnHold,    2, 0);
        kanban.Controls.Add(_colCompleted, 3, 0);

        outerSplit.Controls.Add(kanban, 0, 0);

        _feedPanel = BuildFeedContainer();
        outerSplit.Controls.Add(_feedPanel, 1, 0);
        _projectsTab.Controls.Add(outerSplit);

        // ── Repositories tab ──────────────────────────────────────────────
        _reposTab = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, Visible = false };

        // Status bar
        var repoHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 50,
            BackColor = Color.FromArgb(12, 20, 38),
            Padding   = new Padding(16, 0, 16, 0)
        };
        _repoStatusLbl = new Label
        {
            Text      = "Fetching repositories…",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9.5F),
            ForeColor = TextMuted,
            Location  = new Point(16, 14)
        };
        var syncBtn = new Button
        {
            Text      = "🔄  Sync Now",
            Width     = 110,
            Height    = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 48, 90),
            ForeColor = Accent,
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor    = Cursors.Hand,
            Location  = new Point(870, 10)
        };
        syncBtn.FlatAppearance.BorderSize  = 1;
        syncBtn.FlatAppearance.BorderColor = Accent;
        syncBtn.Click += async (_, _) => await LoadGitHubReposAsync();
        repoHeader.Controls.Add(_repoStatusLbl);
        repoHeader.Controls.Add(syncBtn);
        _reposTab.Controls.Add(repoHeader);

        // Scrollable card grid
        var repoScroll = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = BgPage,
            Padding    = new Padding(16)
        };

        _repoFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Top,
            AutoSize      = true,
            AutoSizeMode  = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = true,
            BackColor     = BgPage,
            Padding       = new Padding(0)
        };
        repoScroll.Controls.Add(_repoFlow);
        _reposTab.Controls.Add(repoScroll);

        // Add both tab panels
        Controls.Add(_reposTab);
        Controls.Add(_projectsTab);

        SwitchTab(projects: true);
    }

    private static Button MakeTabBtn(string text)
    {
        var btn = new Button
        {
            Text      = text,
            Width     = 200,
            Height    = 42,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = TextMuted,
            Font      = new Font("Segoe UI", 9.5F),
            Cursor    = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void SwitchTab(bool projects)
    {
        // Cancel any repo fetch that is still running
        if (!projects)
        {
            _repoCts?.Cancel();
            _repoCts = null;
        }

        _projectsTab.Visible = projects;
        _reposTab.Visible    = !projects;

        _btnProjects.ForeColor = projects ? Accent      : TextMuted;
        _btnProjects.Font      = new Font("Segoe UI", 9.5F, projects ? FontStyle.Bold : FontStyle.Regular);
        _btnRepos.ForeColor    = !projects ? Accent     : TextMuted;
        _btnRepos.Font         = new Font("Segoe UI", 9.5F, !projects ? FontStyle.Bold : FontStyle.Regular);
    }

    // ── GitHub repository loading ─────────────────────────────────────────

    private async Task LoadGitHubReposAsync()
    {
        // Cancel any previous in-flight fetch
        _repoCts?.Cancel();
        _repoCts = new CancellationTokenSource();
        var ct = _repoCts.Token;

        SetRepoStatus("⏳  Connecting to GitHub…", TextMuted);
        _repoFlow.SuspendLayout();
        _repoFlow.Controls.Clear();
        _repoFlow.ResumeLayout(false);

        try
        {
            IGitHubService? svc = null;
            try { svc = ServiceContainer.GetService<IGitHubService>(); }
            catch { /* not wired */ }

            if (svc == null)
            {
                ShowRepoEmpty("GitHub service is not configured.\nGo to Setup → GitHub and connect your account.");
                SetRepoStatus("○  Not connected — add a PAT in Setup → GitHub", AccentAmber);
                return;
            }

            // Test connection first
            var (ok, msg) = await svc.TestConnectionAsync();
            if (!ok)
            {
                ShowRepoEmpty($"Could not connect to GitHub:\n{msg}\n\nCheck your token in Setup → GitHub.");
                SetRepoStatus($"○  {msg}", Color.FromArgb(220, 70, 70));
                return;
            }

            // Fetch all repos for the authenticated user
            var repos = await svc.GetUserRepositoriesAsync(count: 200);

            if (ct.IsCancellationRequested) return;

            if (repos.Count == 0)
            {
                ShowRepoEmpty("No repositories found on your GitHub account.");
                SetRepoStatus("✓  Connected — 0 repositories", AccentGreen);
                return;
            }

            // Sort: recently pushed first
            repos = repos.OrderByDescending(r => r.PushedAt ?? r.UpdatedAt).ToList();

            if (ct.IsCancellationRequested) return;

            if (InvokeRequired) { Invoke(() => PopulateRepoCards(repos, ct)); return; }
            PopulateRepoCards(repos, ct);
        }
        catch (OperationCanceledException) { /* switched away — ignore */ }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                ShowRepoEmpty($"Unexpected error:\n{ex.Message}");
                SetRepoStatus($"✕  Error: {ex.Message}", Color.FromArgb(220, 70, 70));
            }
        }
    }

    private void PopulateRepoCards(List<G_Repos> repos, CancellationToken ct)
    {
        _repoFlow.SuspendLayout();
        _repoFlow.Controls.Clear();
        foreach (var repo in repos)
        {
            if (ct.IsCancellationRequested) break;
            _repoFlow.Controls.Add(BuildRepoCard(repo));
        }
        _repoFlow.ResumeLayout(true);

        var ts = DateTime.Now.ToString("HH:mm:ss");
        SetRepoStatus($"✓  Connected — {repos.Count} repositories  ·  Last synced {ts}", AccentGreen);
    }

    private void SetRepoStatus(string text, Color color)
    {
        if (_repoStatusLbl.IsDisposed) return;
        if (InvokeRequired) { Invoke(() => SetRepoStatus(text, color)); return; }
        _repoStatusLbl.Text      = text;
        _repoStatusLbl.ForeColor = color;
    }

    private void ShowRepoEmpty(string message)
    {
        if (InvokeRequired) { Invoke(() => ShowRepoEmpty(message)); return; }
        _repoFlow.SuspendLayout();
        _repoFlow.Controls.Clear();
        _repoFlow.Controls.Add(new Label
        {
            Text      = message,
            AutoSize  = true,
            Font      = new Font("Segoe UI", 11F),
            ForeColor = TextMuted,
            Padding   = new Padding(24),
            Margin    = new Padding(24)
        });
        _repoFlow.ResumeLayout(true);
    }

    private static Panel BuildRepoCard(G_Repos repo)
    {
        // Language → colour pill
        var langColor = LanguageColor(repo.Language);

        var card = new Panel
        {
            Width     = 280,
            Height    = 170,
            BackColor = BgRepoCard,
            Margin    = new Padding(8),
            Cursor    = Cursors.Hand,
            Padding   = new Padding(14)
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };
        card.MouseEnter += (_, _) => card.BackColor = Color.FromArgb(26, 44, 74);
        card.MouseLeave += (_, _) => card.BackColor = BgRepoCard;
        // Shared action — open repo in browser
        void OpenRepo()
        {
            if (!string.IsNullOrEmpty(repo.HtmlUrl))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    repo.HtmlUrl) { UseShellExecute = true });
        }

        card.Click += (_, _) => OpenRepo();

        // Propagate click/hover to child controls
        void Bubble(Control child)
        {
            child.MouseEnter += (_, _) => card.BackColor = Color.FromArgb(26, 44, 74);
            child.MouseLeave += (_, _) => card.BackColor = BgRepoCard;
            child.Click      += (_, _) => OpenRepo();
        }

        // ── repo name ────────────────────────────────────────────────────
        var nameLbl = new Label
        {
            Text         = repo.Name,
            AutoSize     = false,
            Width        = 240,
            Height       = 24,
            Font         = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor    = Accent,
            Location     = new Point(14, 12),
            AutoEllipsis = true
        };
        Bubble(nameLbl);
        card.Controls.Add(nameLbl);

        // Private / Fork badge
        if (repo.IsPrivate || repo.IsFork)
        {
            var badge = new Label
            {
                Text      = repo.IsPrivate ? "🔒 Private" : "🍴 Fork",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = repo.IsPrivate
                    ? Color.FromArgb(80, 30, 30)
                    : Color.FromArgb(30, 50, 90),
                Location  = new Point(14, 38),
                Padding   = new Padding(5, 2, 5, 2)
            };
            Bubble(badge);
            card.Controls.Add(badge);
        }

        // ── description ──────────────────────────────────────────────────
        var descLbl = new Label
        {
            Text         = string.IsNullOrWhiteSpace(repo.Description) ? "(no description)" : repo.Description,
            AutoSize     = false,
            Width        = 252,
            Height       = 34,
            Font         = new Font("Segoe UI", 8.5F),
            ForeColor    = TextMuted,
            Location     = new Point(14, 60),
            AutoEllipsis = true
        };
        Bubble(descLbl);
        card.Controls.Add(descLbl);

        // ── language ─────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(repo.Language))
        {
            var dot = new Panel
            {
                Width     = 10,
                Height    = 10,
                BackColor = langColor,
                Location  = new Point(14, 100)
            };
            dot.Region = new Region(new System.Drawing.Drawing2D.GraphicsPath());
            dot.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(langColor);
                e.Graphics.FillEllipse(br, 0, 0, 9, 9);
            };
            Bubble(dot);
            card.Controls.Add(dot);

            var langLbl = new Label
            {
                Text      = repo.Language,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = TextMuted,
                Location  = new Point(28, 97)
            };
            Bubble(langLbl);
            card.Controls.Add(langLbl);
        }

        // ── stats row ────────────────────────────────────────────────────
        var statsFlow = new FlowLayoutPanel
        {
            AutoSize      = true,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor     = Color.Transparent,
            Location      = new Point(12, 116),
            WrapContents  = false
        };

        void AddStat(string icon, int val, Color col)
        {
            statsFlow.Controls.Add(new Label
            {
                Text      = $"{icon} {val}",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = col,
                Margin    = new Padding(0, 0, 10, 0)
            });
        }

        AddStat("⭐", repo.Stars,      Color.FromArgb(220, 185, 60));
        AddStat("⑂",  repo.Forks,      TextMuted);
        AddStat("⚠",  repo.OpenIssues, repo.OpenIssues > 0 ? AccentAmber : TextMuted);

        Bubble(statsFlow);
        card.Controls.Add(statsFlow);

        // ── push date ────────────────────────────────────────────────────
        var pushed  = repo.PushedAt ?? repo.UpdatedAt;
        var ageDays = (DateTime.UtcNow - pushed.ToUniversalTime()).TotalDays;
        var ageStr  = ageDays < 1   ? "today"
                    : ageDays < 2   ? "yesterday"
                    : ageDays < 30  ? $"{(int)ageDays}d ago"
                    : ageDays < 365 ? $"{(int)(ageDays/30)}mo ago"
                    : $"{(int)(ageDays/365)}y ago";

        var dateLbl = new Label
        {
            Text      = $"Pushed {ageStr}",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 7.5F),
            ForeColor = TextMuted,
            Location  = new Point(14, 148)
        };
        Bubble(dateLbl);
        card.Controls.Add(dateLbl);

        return card;
    }

    // GitHub-style language → colour mapping
    private static Color LanguageColor(string? lang) => lang switch
    {
        "C#"         => Color.FromArgb(119, 62, 200),
        "JavaScript" => Color.FromArgb(241, 224, 90),
        "TypeScript" => Color.FromArgb(49, 120, 198),
        "Python"     => Color.FromArgb(53, 114, 165),
        "Java"       => Color.FromArgb(176, 114, 25),
        "Go"         => Color.FromArgb(0,  173, 216),
        "Rust"       => Color.FromArgb(222, 165, 132),
        "C++"        => Color.FromArgb(243, 75,  125),
        "C"          => Color.FromArgb(85, 85, 85),
        "PHP"        => Color.FromArgb(79, 93, 149),
        "Ruby"       => Color.FromArgb(112, 21, 22),
        "Swift"      => Color.FromArgb(240, 81, 56),
        "Kotlin"     => Color.FromArgb(169, 123, 255),
        "Dart"       => Color.FromArgb(0,  180, 216),
        "HTML"       => Color.FromArgb(228, 75, 35),
        "CSS"        => Color.FromArgb(86, 61, 124),
        "Shell"      => Color.FromArgb(137, 224, 81),
        _            => Color.FromArgb(100, 130, 180)
    };

    // ── Kanban helpers (unchanged) ────────────────────────────────────────

    private static Panel BuildColumn()
    {
        return new Panel
        {
            Dock       = DockStyle.Fill,
            BackColor  = BgColumn,
            Margin     = new Padding(4),
            AutoScroll = true
        };
    }

    private Panel BuildFeedContainer()
    {
        var outer = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = BgFeed,
            Margin    = new Padding(6, 12, 12, 12)
        };
        outer.Controls.Add(new Label
        {
            Text      = "   Recent Activity",
            AutoSize  = false,
            Dock      = DockStyle.Top,
            Height    = 42,
            Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = Color.FromArgb(10, 18, 34),
            TextAlign = ContentAlignment.MiddleLeft
        });
        var scroll = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = BgFeed
        };
        scroll.Tag = "feedScroll";
        outer.Controls.Add(scroll);
        return outer;
    }

    private static void PopulateColumn(Panel col, string title, Color accentColor, List<ProjectRecord> projects)
    {
        col.SuspendLayout();
        col.Controls.Clear();

        var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(12, 20, 38) };
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(accentColor, 3);
            e.Graphics.DrawLine(pen, 0, 0, header.Width, 0);
        };

        header.Controls.Add(new Label
        {
            Text      = $"  {title}",
            AutoSize  = false,
            Dock      = DockStyle.Left,
            Width     = 140,
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = accentColor,
            TextAlign = ContentAlignment.MiddleLeft
        });
        header.Controls.Add(new Label
        {
            Text      = projects.Count.ToString(),
            AutoSize  = false,
            Dock      = DockStyle.Right,
            Width     = 32,
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 130, 170),
            TextAlign = ContentAlignment.MiddleRight,
            Padding   = new Padding(0, 0, 8, 0)
        });
        col.Controls.Add(header);

        foreach (var card in projects.Select(BuildProjectCard).Reverse())
            col.Controls.Add(card);

        col.ResumeLayout(true);
    }

    private static Panel BuildProjectCard(ProjectRecord p)
    {
        var card = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 88,
            BackColor = BgCard,
            Margin    = new Padding(6, 0, 6, 6),
            Padding   = new Padding(10, 8, 10, 8),
            Cursor    = Cursors.Hand
        };
        card.MouseEnter += (_, _) => card.BackColor = Color.FromArgb(28, 46, 78);
        card.MouseLeave += (_, _) => card.BackColor = BgCard;

        card.Controls.Add(new Label
        {
            Text      = p.ProjectName,
            AutoSize  = false,
            Width     = 180,
            Height    = 32,
            Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location  = new Point(10, 8),
            AutoEllipsis = true
        });
        card.Controls.Add(new Label
        {
            Text      = p.Client,
            AutoSize  = false,
            Width     = 180,
            Height    = 18,
            Font      = new Font("Segoe UI", 8.5F),
            ForeColor = TextMuted,
            Location  = new Point(10, 38)
        });

        if (PriorityColors.TryGetValue(p.Priority, out var pillColor))
            card.Controls.Add(new Label
            {
                Text      = p.Priority,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = pillColor,
                Location  = new Point(10, 62),
                Padding   = new Padding(6, 2, 6, 2)
            });

        card.Controls.Add(new Label
        {
            Text      = p.UpdatedAt.ToString("MMM d"),
            AutoSize  = true,
            Font      = new Font("Segoe UI", 7.5F),
            ForeColor = TextMuted,
            Location  = new Point(130, 66)
        });

        return card;
    }

    private void PopulateFeed(List<ActivityEntry> entries)
    {
        var scroll = _feedPanel.Controls.OfType<Panel>().FirstOrDefault(p => p.Tag?.ToString() == "feedScroll");
        if (scroll == null) return;

        scroll.SuspendLayout();
        scroll.Controls.Clear();

        if (entries.Count == 0)
        {
            scroll.Controls.Add(new Label
            {
                Text      = "No activity recorded yet.\nSave or delete a project to\nsee changes here.",
                AutoSize  = false,
                Dock      = DockStyle.Top,
                Height    = 80,
                Font      = new Font("Segoe UI", 9.5F),
                ForeColor = TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding   = new Padding(12)
            });
            scroll.ResumeLayout(true);
            return;
        }

        foreach (var entry in entries.AsEnumerable().Reverse())
            scroll.Controls.Add(BuildFeedRow(entry));

        scroll.ResumeLayout(true);
        scroll.AutoScrollPosition = new Point(0, 0);
    }

    private static Panel BuildFeedRow(ActivityEntry entry)
    {
        ActionMeta.TryGetValue(entry.Action, out var meta);
        var (icon, color) = meta.icon != null ? meta : ("·", TextMuted);

        var row = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 68,
            BackColor = Color.Transparent
        };

        row.Controls.Add(new Panel { Width = 3, Dock = DockStyle.Left, BackColor = color });
        row.Controls.Add(new Label
        {
            Text      = icon,
            AutoSize  = false,
            Width     = 28,
            Height    = 68,
            Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = color,
            TextAlign = ContentAlignment.MiddleCenter,
            Location  = new Point(3, 0)
        });

        row.Controls.Add(new Label
        {
            Text         = entry.ProjectName,
            AutoSize     = false,
            Width        = 220,
            Height       = 22,
            Font         = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor    = TextPrimary,
            Location     = new Point(34, 8),
            AutoEllipsis = true
        });
        row.Controls.Add(new Label
        {
            Text      = $"{entry.Action}  {(string.IsNullOrEmpty(entry.Detail) ? "" : "· " + entry.Detail)}",
            AutoSize  = false,
            Width     = 220,
            Height    = 20,
            Font      = new Font("Segoe UI", 8.5F),
            ForeColor = color,
            Location  = new Point(34, 28)
        });
        row.Controls.Add(new Label
        {
            Text      = entry.Client,
            AutoSize  = false,
            Width     = 140,
            Height    = 18,
            Font      = new Font("Segoe UI", 8.5F),
            ForeColor = TextMuted,
            Location  = new Point(34, 46)
        });
        row.Controls.Add(new Label
        {
            Text      = entry.Timestamp.ToString("MMM d, HH:mm"),
            AutoSize  = false,
            Width     = 100,
            Height    = 18,
            Font      = new Font("Segoe UI", 7.5F),
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleRight,
            Location  = new Point(160, 46)
        });

        row.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(28, 46, 78), 1);
            e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };

        return row;
    }
}
