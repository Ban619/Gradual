using Gradual.Models.GitHub;
using Gradual.Repositories.GitHub;
using Gradual.Services.GitHub;

namespace Gradual.Forms;

/// <summary>
/// Repository panel — card grid of GitHub repos (top) + recent commits feed (bottom).
/// Matches the Project Management reference layout adapted for GitHub data.
/// </summary>
public class RepositoryPanel : UserControl
{
    // ── palette ─────────────────────────────────────────────────────────
    private static readonly Color BgPage      = Color.FromArgb(10, 18, 34);
    private static readonly Color BgCard      = Color.FromArgb(16, 26, 46);
    private static readonly Color BgCardHov   = Color.FromArgb(22, 36, 62);
    private static readonly Color BgSection   = Color.FromArgb(13, 22, 40);
    private static readonly Color AccentBlue  = Color.FromArgb(94, 129, 255);
    private static readonly Color AccentGreen = Color.FromArgb(49, 196, 141);
    private static readonly Color AccentAmber = Color.FromArgb(247, 173, 64);
    private static readonly Color AccentRed   = Color.FromArgb(220, 70, 70);
    private static readonly Color TextPrimary = Color.FromArgb(220, 233, 255);
    private static readonly Color TextMuted   = Color.FromArgb(120, 150, 190);
    private static readonly Color Border      = Color.FromArgb(28, 46, 78);

    // ── language colour map ──────────────────────────────────────────────
    private static readonly Dictionary<string, Color> LangColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C#"]         = Color.FromArgb(104, 33, 122),
        ["JavaScript"] = Color.FromArgb(241, 224, 90),
        ["TypeScript"] = Color.FromArgb(49, 120, 198),
        ["Python"]     = Color.FromArgb(53, 114, 165),
        ["Go"]         = Color.FromArgb(0, 173, 216),
        ["Rust"]       = Color.FromArgb(222, 165, 132),
        ["Java"]       = Color.FromArgb(176, 114, 25),
        ["Dart"]       = Color.FromArgb(0, 180, 216),
        ["Swift"]      = Color.FromArgb(240, 81, 56),
        ["Kotlin"]     = Color.FromArgb(127, 82, 255),
        ["HTML"]       = Color.FromArgb(227, 76, 38),
        ["CSS"]        = Color.FromArgb(86, 61, 124),
        ["Shell"]      = Color.FromArgb(137, 224, 81),
    };

    // ── services ────────────────────────────────────────────────────────
    private readonly IGitHubService?        _gitHubService;
    private readonly IG_ReposStore? _store;

    // ── UI regions ───────────────────────────────────────────────────────
    private Panel            _cardGrid    = null!;
    private Panel            _commitFeed  = null!;
    private Label            _repoCount   = null!;
    private Label            _commitCount = null!;
    private Label            _statusLbl   = null!;

    public RepositoryPanel(IGitHubService? gitHubService = null,
                           IG_ReposStore? store  = null)
    {
        _gitHubService = gitHubService;
        _store         = store;

        BackColor  = BgPage;
        Dock       = DockStyle.Fill;
        AutoScroll = false;
        BuildLayout();
    }

    // ── public API ────────────────────────────────────────────────────────

    public async Task RefreshAsync()
    {
        SetStatus("Loading…");
        try
        {
            List<G_Repos> repos   = new();
            List<G_Commit>     commits = new();

            if (_store != null)
            {
                repos   = await _store.GetAllRepositoriesAsync();
                foreach (var r in repos.Take(5))
                    commits.AddRange(await _store.GetCommitsByRepositoryAsync(r.Id, 5));
            }

            // If the local store is empty, fall back to the live GitHub API
            // so the panel shows repos immediately without waiting for a sync.
            if (repos.Count == 0 && _gitHubService != null)
            {
                var (ok, _) = await _gitHubService.TestConnectionAsync();
                if (ok)
                    repos = await _gitHubService.GetUserRepositoriesAsync(count: 100);
            }

            commits = commits
                .OrderByDescending(c => c.CommittedAt)
                .Take(20)
                .ToList();

            PopulateCards(repos);
            PopulateCommits(commits);
            SetStatus($"{repos.Count} repositor{(repos.Count == 1 ? "y" : "ies")} · {commits.Count} recent commits");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading repositories: {ex.Message}");
        }
    }

    // ── layout build ─────────────────────────────────────────────────────

    private void BuildLayout()
    {
        // ── header ───────────────────────────────────────────────────────
        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 70,
            BackColor = Color.FromArgb(8, 16, 30),
            Padding   = new Padding(24, 0, 24, 0)
        };

        var title = new Label
        {
            Text      = "Repository",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location  = new Point(24, 14)
        };
        header.Controls.Add(title);

        _statusLbl = new Label
        {
            Text      = "Loading…",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9F),
            ForeColor = TextMuted,
            Location  = new Point(26, 46)
        };
        header.Controls.Add(_statusLbl);

        var refreshBtn = new Button
        {
            Text      = "⟳  Refresh",
            Width     = 100,
            Height    = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(25, 42, 72),
            ForeColor = Color.FromArgb(160, 185, 220),
            Font      = new Font("Segoe UI", 9F),
            Cursor    = Cursors.Hand,
            Location  = new Point(780, 20)
        };
        refreshBtn.FlatAppearance.BorderColor = Border;
        refreshBtn.FlatAppearance.BorderSize  = 1;
        refreshBtn.Click += async (_, _) => await RefreshAsync();
        header.Controls.Add(refreshBtn);

        Controls.Add(header);

        // ── outer scroll wrapper ─────────────────────────────────────────
        var scroll = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = BgPage,
            Padding    = new Padding(0)
        };

        // ── inner stack panel ────────────────────────────────────────────
        var stack = new FlowLayoutPanel
        {
            Dock          = DockStyle.Top,
            AutoSize      = true,
            AutoSizeMode  = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            BackColor     = BgPage,
            Padding       = new Padding(20, 16, 20, 20)
        };

        // ── section: Active Repositories ────────────────────────────────
        var repoSection = BuildSectionHeader("📦  Active Repositories", out _repoCount);
        repoSection.Width = 1000;
        stack.Controls.Add(repoSection);

        _cardGrid = new Panel
        {
            Width     = 1000,
            Height    = 210,
            BackColor = BgPage,
            Margin    = new Padding(0, 0, 0, 24)
        };
        stack.Controls.Add(_cardGrid);

        // ── section: Recent Commits ──────────────────────────────────────
        var commitSection = BuildSectionHeader("🕒  Recent Commits", out _commitCount);
        commitSection.Width = 1000;
        stack.Controls.Add(commitSection);

        _commitFeed = new Panel
        {
            Width     = 1000,
            Height    = 10,  // grows as rows are added
            BackColor = BgPage,
            Margin    = new Padding(0)
        };
        stack.Controls.Add(_commitFeed);

        scroll.Controls.Add(stack);
        Controls.Add(scroll);
    }

    private static Panel BuildSectionHeader(string text, out Label badge)
    {
        var p = new Panel
        {
            Height    = 36,
            BackColor = BgPage,
            Margin    = new Padding(0, 0, 0, 8)
        };

        var lbl = new Label
        {
            Text      = text,
            AutoSize  = true,
            Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location  = new Point(0, 6)
        };
        p.Controls.Add(lbl);

        badge = new Label
        {
            Text      = "0",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 130, 170),
            Location  = new Point(220, 9)
        };
        p.Controls.Add(badge);

        return p;
    }

    // ── card grid population ─────────────────────────────────────────────

    private void PopulateCards(List<G_Repos> repos)
    {
        _cardGrid.SuspendLayout();
        _cardGrid.Controls.Clear();

        if (repos.Count == 0)
        {
            var empty = new Label
            {
                Text      = "No repositories found.\nConnect a GitHub account or link a repository to a project.",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 10F),
                ForeColor = TextMuted,
                Location  = new Point(0, 60)
            };
            _cardGrid.Controls.Add(empty);
            _cardGrid.Height = 180;
            _repoCount.Text  = "0";
            _cardGrid.ResumeLayout(true);
            return;
        }

        const int CardW   = 240;
        const int CardH   = 190;
        const int CardGap = 16;
        int col = 0;

        foreach (var repo in repos)
        {
            var card = BuildRepoCard(repo);
            card.Location = new Point(col * (CardW + CardGap), 0);
            _cardGrid.Controls.Add(card);
            col++;
        }

        _cardGrid.Height = CardH + 8;
        _repoCount.Text  = repos.Count.ToString();
        _cardGrid.ResumeLayout(true);
    }

    private static Panel BuildRepoCard(G_Repos repo)
    {
        var card = new Panel
        {
            Width     = 240,
            Height    = 190,
            BackColor = BgCard,
            Cursor    = Cursors.Hand
        };

        // Rounded look via Paint
        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        card.MouseEnter += (_, _) => card.BackColor = BgCardHov;
        card.MouseLeave += (_, _) => card.BackColor = BgCard;

        // ── repo name ────────────────────────────────────────────────────
        var nameLbl = new Label
        {
            Text         = repo.Name,
            AutoSize     = false,
            Width        = 220,
            Height       = 24,
            Font         = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor    = AccentBlue,
            Location     = new Point(14, 14),
            AutoEllipsis = true
        };
        card.Controls.Add(nameLbl);

        // ── owner ────────────────────────────────────────────────────────
        var ownerLbl = new Label
        {
            Text      = repo.Owner,
            AutoSize  = false,
            Width     = 220,
            Height    = 18,
            Font      = new Font("Segoe UI", 8.5F),
            ForeColor = TextMuted,
            Location  = new Point(14, 36)
        };
        card.Controls.Add(ownerLbl);

        // ── description ─────────────────────────────────────────────────
        var descLbl = new Label
        {
            Text         = string.IsNullOrEmpty(repo.Description) ? "No description" : repo.Description,
            AutoSize     = false,
            Width        = 212,
            Height       = 42,
            Font         = new Font("Segoe UI", 8.5F),
            ForeColor    = TextMuted,
            Location     = new Point(14, 60),
            AutoEllipsis = true
        };
        card.Controls.Add(descLbl);

        // ── language dot + label ─────────────────────────────────────────
        if (!string.IsNullOrEmpty(repo.Language))
        {
            var dotColor = LangColors.TryGetValue(repo.Language, out var lc) ? lc : AccentBlue;

            var dot = new Panel
            {
                Width     = 10,
                Height    = 10,
                BackColor = dotColor,
                Location  = new Point(14, 112)
            };
            dot.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(dotColor), 0, 0, 9, 9);
            };
            card.Controls.Add(dot);

            var langLbl = new Label
            {
                Text      = repo.Language,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(180, 200, 230),
                Location  = new Point(28, 108)
            };
            card.Controls.Add(langLbl);
        }

        // ── stats row ────────────────────────────────────────────────────
        var statsLbl = new Label
        {
            Text      = $"⭐ {repo.Stars}   🍴 {repo.Forks}   ⚠ {repo.OpenIssues}",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 8.5F),
            ForeColor = TextMuted,
            Location  = new Point(14, 132)
        };
        card.Controls.Add(statsLbl);

        // ── sync status pill ─────────────────────────────────────────────
        var syncColor = repo.SyncStatus switch
        {
            "Success" => AccentGreen,
            "Failed"  => AccentRed,
            _         => AccentAmber
        };

        var syncPill = new Label
        {
            Text      = repo.SyncStatus,
            AutoSize  = true,
            Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = syncColor,
            Location  = new Point(14, 158),
            Padding   = new Padding(6, 2, 6, 2)
        };
        card.Controls.Add(syncPill);

        // ── privacy badge ────────────────────────────────────────────────
        if (repo.IsPrivate)
        {
            var privLbl = new Label
            {
                Text      = "🔒 Private",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(150, 170, 210),
                Location  = new Point(100, 162)
            };
            card.Controls.Add(privLbl);
        }

        // ── last pushed ──────────────────────────────────────────────────
        var dateLbl = new Label
        {
            Text      = repo.PushedAt.HasValue
                ? $"Pushed {FormatRelative(repo.PushedAt.Value)}"
                : $"Updated {FormatRelative(repo.UpdatedAt)}",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 7.5F),
            ForeColor = TextMuted,
            Location  = new Point(14, 172)
        };
        card.Controls.Add(dateLbl);

        return card;
    }

    // ── commit feed population ────────────────────────────────────────────

    private void PopulateCommits(List<G_Commit> commits)
    {
        _commitFeed.SuspendLayout();
        _commitFeed.Controls.Clear();
        _commitCount.Text = commits.Count.ToString();

        if (commits.Count == 0)
        {
            var empty = new Label
            {
                Text      = "No recent commits found.",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9.5F),
                ForeColor = TextMuted,
                Location  = new Point(0, 12)
            };
            _commitFeed.Controls.Add(empty);
            _commitFeed.Height = 48;
            _commitFeed.ResumeLayout(true);
            return;
        }

        int y = 0;
        foreach (var commit in commits)
        {
            var row = BuildCommitRow(commit);
            row.Location = new Point(0, y);
            _commitFeed.Controls.Add(row);
            y += row.Height;
        }

        _commitFeed.Height = y;
        _commitFeed.ResumeLayout(true);
    }

    private static Panel BuildCommitRow(G_Commit commit)
    {
        var row = new Panel
        {
            Width     = 980,
            Height    = 52,
            BackColor = BgSection,
            Cursor    = Cursors.Hand
        };
        row.MouseEnter += (_, _) => row.BackColor = BgCard;
        row.MouseLeave += (_, _) => row.BackColor = BgSection;

        // commit icon circle
        var icon = new Label
        {
            Text      = "●",
            AutoSize  = false,
            Width     = 32,
            Height    = 52,
            Font      = new Font("Segoe UI", 11F),
            ForeColor = AccentBlue,
            TextAlign = ContentAlignment.MiddleCenter,
            Location  = new Point(0, 0)
        };
        row.Controls.Add(icon);

        // message
        var msgFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        var msg = commit.Message.Split('\n')[0]; // first line only
        var msgLbl = new Label
        {
            Text         = msg,
            AutoSize     = false,
            Width        = 540,
            Height       = 24,
            Font         = msgFont,
            ForeColor    = TextPrimary,
            Location     = new Point(36, 8),
            AutoEllipsis = true
        };
        row.Controls.Add(msgLbl);

        // author + time
        var metaLbl = new Label
        {
            Text      = $"{commit.AuthorName}  ·  {FormatRelative(commit.CommittedAt)}",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 8F),
            ForeColor = TextMuted,
            Location  = new Point(36, 30)
        };
        row.Controls.Add(metaLbl);

        // SHA chip
        var sha = commit.Sha.Length >= 7 ? commit.Sha[..7] : commit.Sha;
        var shaLbl = new Label
        {
            Text      = sha,
            AutoSize  = true,
            Font      = new Font("Cascadia Code", 8F),
            ForeColor = Color.FromArgb(100, 140, 200),
            BackColor = Color.FromArgb(20, 34, 58),
            Location  = new Point(600, 17),
            Padding   = new Padding(5, 2, 5, 2)
        };
        row.Controls.Add(shaLbl);

        // branch
        if (!string.IsNullOrEmpty(commit.BranchName))
        {
            var branchLbl = new Label
            {
                Text      = $"⎇ {commit.BranchName}",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(120, 150, 190),
                Location  = new Point(700, 17)
            };
            row.Controls.Add(branchLbl);
        }

        // additions/deletions
        if (commit.Additions > 0 || commit.Deletions > 0)
        {
            var diffLbl = new Label
            {
                Text      = $"+{commit.Additions} −{commit.Deletions}",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = AccentGreen,
                Location  = new Point(820, 17)
            };
            row.Controls.Add(diffLbl);
        }

        // bottom separator
        row.Paint += (_, e) =>
        {
            using var pen = new Pen(Border, 1);
            e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };

        return row;
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static string FormatRelative(DateTime dt)
    {
        var diff = DateTime.Now - dt;
        return diff.TotalMinutes < 1  ? "just now"
             : diff.TotalHours   < 1  ? $"{(int)diff.TotalMinutes}m ago"
             : diff.TotalDays    < 1  ? $"{(int)diff.TotalHours}h ago"
             : diff.TotalDays    < 7  ? $"{(int)diff.TotalDays}d ago"
             : dt.ToString("MMM d, yyyy");
    }

    private void SetStatus(string text)
    {
        if (_statusLbl.InvokeRequired)
            _statusLbl.Invoke(() => _statusLbl.Text = text);
        else
            _statusLbl.Text = text;
    }
}
