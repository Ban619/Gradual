using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using Gradual.Configuration;
using Gradual.Services;

namespace Gradual.Forms;

/// <summary>
/// Setup panel — VS Code–style settings UI.
/// Left: category nav  |  Right: settings form for selected category.
/// Reads from appsettings.json and writes changes back on Save.
/// </summary>
public class SetupPanel : UserControl
{
    // ── palette ──────────────────────────────────────────────────────────
    private static readonly Color BgPage     = Color.FromArgb(10, 18, 34);
    private static readonly Color BgNav      = Color.FromArgb(8, 16, 30);
    private static readonly Color BgContent  = Color.FromArgb(14, 24, 42);
    private static readonly Color BgField    = Color.FromArgb(20, 34, 58);
    private static readonly Color BgCard     = Color.FromArgb(16, 28, 50);
    private static readonly Color AccentBlue = Color.FromArgb(94, 129, 255);
    private static readonly Color AccentGreen= Color.FromArgb(49, 196, 141);
    private static readonly Color AccentAmber= Color.FromArgb(247, 173, 64);
    private static readonly Color AccentRed  = Color.FromArgb(220, 70, 70);
    private static readonly Color TextPrimary= Color.FromArgb(220, 233, 255);
    private static readonly Color TextMuted  = Color.FromArgb(120, 150, 190);
    private static readonly Color Border     = Color.FromArgb(28, 46, 78);

    // ── state ─────────────────────────────────────────────────────────────
    private readonly ConfigurationService _config;
    private readonly string _settingsFilePath;
    private Panel _contentArea = null!;
    private Button? _activeNavBtn;
    private string _currentSection = "General";

    // ── field controls we need to read back on Save ───────────────────────
    private readonly Dictionary<string, Control> _fields = new();

    public SetupPanel(ConfigurationService config)
    {
        _config           = config;
        _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        BackColor         = BgPage;
        Dock              = DockStyle.Fill;
        BuildLayout();
        ShowSection("General");
    }

    // ── layout ────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        // ── page header ──────────────────────────────────────────────────
        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 60,
            BackColor = Color.FromArgb(8, 16, 30),
            Padding   = new Padding(24, 0, 24, 0)
        };
        var titleLbl = new Label
        {
            Text      = "Setup",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location  = new Point(24, 14)
        };
        header.Controls.Add(titleLbl);
        Controls.Add(header);

        // ── split: nav | content ─────────────────────────────────────────
        var split = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 1,
            BackColor   = BgPage,
            Padding     = new Padding(0)
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100F));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // ── left nav ─────────────────────────────────────────────────────
        var nav = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = BgNav,
            Padding   = new Padding(0, 12, 0, 12)
        };

        var navItems = new[]
        {
            ("⚙️",  "General"),
            ("🐙",  "GitHub"),
            ("📊",  "Analytics"),
            ("🔔",  "Webhooks"),
            ("📋",  "Validation"),
        };

        int navY = 12;
        foreach (var (icon, label) in navItems)
        {
            var btn = BuildNavBtn(icon, label);
            btn.Location = new Point(0, navY);
            btn.Click   += (_, _) => ShowSection(label);
            nav.Controls.Add(btn);
            navY += btn.Height;
        }

        split.Controls.Add(nav, 0, 0);

        // ── right content ─────────────────────────────────────────────────
        _contentArea = new Panel
        {
            Dock       = DockStyle.Fill,
            BackColor  = BgContent,
            AutoScroll = true,
            Padding    = new Padding(32, 24, 32, 24)
        };
        split.Controls.Add(_contentArea, 1, 0);

        Controls.Add(split);
    }

    private Button BuildNavBtn(string icon, string label)
    {
        var btn = new Button
        {
            Text      = $"  {icon}  {label}",
            Width     = 200,
            Height    = 44,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = TextMuted,
            Font      = new Font("Segoe UI", 10F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(10, 0, 0, 0),
            Cursor    = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize  = 0;
        btn.FlatAppearance.BorderColor = BgNav;
        btn.FlatAppearance.MouseOverBackColor  = Color.FromArgb(18, 32, 56);
        btn.FlatAppearance.MouseDownBackColor  = Color.FromArgb(20, 36, 62);
        btn.MouseEnter += (_, _) => { if (btn != _activeNavBtn) btn.BackColor = Color.FromArgb(18, 32, 56); };
        btn.MouseLeave += (_, _) => { if (btn != _activeNavBtn) btn.BackColor = Color.Transparent; };
        return btn;
    }

    // ── section rendering ─────────────────────────────────────────────────

    private void ShowSection(string section)
    {
        // Cancel any in-flight GitHub connection test from the previous view
        _ghCts?.Cancel();
        _ghCts = null;

        // Null out GitHub UI refs so stale async callbacks don't touch disposed controls
        _ghProfileCard = null;
        _ghStatusBadge = null;
        _ghUserName    = null;
        _ghUserLogin   = null;
        _ghUserMeta    = null;
        _ghAvatar      = null;
        _ghConnectBtn  = null;
        _ghPatBox      = null;

        _currentSection = section;
        _fields.Clear();
        _contentArea.SuspendLayout();
        _contentArea.Controls.Clear();

        // Update nav active state
        foreach (var btn in _contentArea.Parent?.Parent?.Controls
            .OfType<TableLayoutPanel>().FirstOrDefault()
            ?.GetControlFromPosition(0, 0)?.Controls.OfType<Button>()
            ?? Enumerable.Empty<Button>())
        {
            btn.BackColor = btn.Text.Contains(section)
                ? Color.FromArgb(18, 32, 58)
                : Color.Transparent;
            btn.ForeColor = btn.Text.Contains(section) ? AccentBlue : TextMuted;
            btn.Font      = new Font("Segoe UI", 10F,
                btn.Text.Contains(section) ? FontStyle.Bold : FontStyle.Regular);
            if (btn.Text.Contains(section)) _activeNavBtn = btn;
        }

        // Build the form content
        var stack = new FlowLayoutPanel
        {
            Dock          = DockStyle.Top,
            AutoSize      = true,
            AutoSizeMode  = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            BackColor     = BgContent,
            Padding       = new Padding(0)
        };

        switch (section)
        {
            case "General":  BuildGeneralSection(stack);    break;
            case "GitHub":   BuildGitHubSection(stack);     break;
            case "Analytics":BuildAnalyticsSection(stack);  break;
            case "Webhooks": BuildWebhooksSection(stack);   break;
            case "Validation":BuildValidationSection(stack);break;
        }

        // Save + Reset buttons
        AddActionButtons(stack);

        _contentArea.Controls.Add(stack);
        _contentArea.ResumeLayout(true);
    }

    // ── SECTION: General ──────────────────────────────────────────────────

    private void BuildGeneralSection(FlowLayoutPanel stack)
    {
        AddSectionTitle(stack, "⚙️  General", "Application name, data storage, and backup settings.");
        AddCard(stack, "Application", new[]
        {
            MakeTextRow("Application Name",    "AppName",          _config.Settings.ApplicationSettings.ApplicationName,    "Display name for the application"),
            MakeTextRow("Data Directory",      "DataDir",          _config.Settings.ApplicationSettings.DataDirectory,      "Folder where project data is stored"),
            MakeTextRow("Backup Directory",    "BackupDir",        _config.Settings.ApplicationSettings.BackupDirectory,    "Folder where backups are saved"),
        });
        AddCard(stack, "Auto Backup", new[]
        {
            MakeToggleRow("Enable Auto Backup","AutoBackup",       _config.Settings.ApplicationSettings.EnableAutoBackup,   "Automatically save backups at regular intervals"),
            MakeNumberRow("Backup Interval (minutes)","BackupInterval", _config.Settings.ApplicationSettings.BackupIntervalMinutes, "How often to auto-backup (minutes)"),
            MakeNumberRow("Max Backup Files",  "MaxBackups",       _config.Settings.ApplicationSettings.MaxBackupFiles,     "Maximum number of backup files to keep"),
        });
    }

    // ── SECTION: GitHub ───────────────────────────────────────────────────

    // Holds live-connection UI refs so the async callback can update them
    private Panel?    _ghProfileCard   = null;
    private Label?    _ghStatusBadge   = null;
    private Label?    _ghUserName      = null;
    private Label?    _ghUserLogin     = null;
    private Label?    _ghUserMeta      = null;
    private PictureBox? _ghAvatar      = null;
    private Button?   _ghConnectBtn    = null;
    private TextBox?  _ghPatBox        = null;
    // CancellationToken so navigating away kills an in-flight connection test
    private CancellationTokenSource? _ghCts = null;

    private void BuildGitHubSection(FlowLayoutPanel stack)
    {
        AddSectionTitle(stack, "🐙  GitHub Connection",
            "Enter your Personal Access Token to connect Gradual to your GitHub account.");

        // ── Step 1: Connection Card ─────────────────────────────────────────
        var connCard = new Panel
        {
            Width     = 740,
            BackColor = BgCard,
            Height    = 190,
            Margin    = new Padding(0, 0, 0, 16)
        };
        connCard.Paint += (_, e) =>
        {
            using var pen = new Pen(Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, connCard.Width - 1, connCard.Height - 1);
        };

        // Card title bar
        var connHeader = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(12, 22, 40) };
        connHeader.Controls.Add(new Label
        {
            Text      = "Authentication",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(160, 185, 220),
            Location  = new Point(16, 10)
        });

        // "Get a token" link on the right of the header
        var getTokenLink = new LinkLabel
        {
            Text      = "Create a PAT on GitHub →",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 8.5F),
            LinkColor = AccentBlue,
            Location  = new Point(520, 11)
        };
        getTokenLink.Click += (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://github.com/settings/tokens/new?scopes=repo,read:user&description=Gradual")
                { UseShellExecute = true });
        connHeader.Controls.Add(getTokenLink);
        connCard.Controls.Add(connHeader);

        // PAT label
        var patLabel = new Label
        {
            Text      = "Personal Access Token",
            AutoSize  = false,
            Width     = 200,
            Height    = 22,
            Font      = new Font("Segoe UI", 9.5F),
            ForeColor = TextPrimary,
            Location  = new Point(16, 52)
        };

        // PAT textbox
        _ghPatBox = new TextBox
        {
            Text          = _config.GetGitHubPat(),
            Width         = 350,
            Height        = 28,
            BackColor     = BgField,
            ForeColor     = TextPrimary,
            BorderStyle   = BorderStyle.FixedSingle,
            Font          = new Font("Segoe UI", 9.5F),
            PasswordChar  = '●',
            Location      = new Point(220, 50)
        };
        _fields["GitHubPAT"] = _ghPatBox;

        // Eye-toggle
        var eyeBtn = new Button
        {
            Text      = "👁",
            Width     = 30,
            Height    = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = BgField,
            ForeColor = TextMuted,
            Cursor    = Cursors.Hand,
            Location  = new Point(574, 50)
        };
        eyeBtn.FlatAppearance.BorderSize = 0;
        eyeBtn.Click += (_, _) => _ghPatBox.PasswordChar = _ghPatBox.PasswordChar == '\0' ? '●' : '\0';

        // Hint
        var patHint = new Label
        {
            Text      = "Required scopes: repo · read:user",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 7.5F),
            ForeColor = TextMuted,
            Location  = new Point(220, 82)
        };

        // Status badge
        _ghStatusBadge = new Label
        {
            Text      = "● Not connected",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(130, 150, 180),
            Location  = new Point(220, 106)
        };
        UpdateConnectionBadge(false, null);

        // Connect button
        _ghConnectBtn = new Button
        {
            Text      = "🔗  Connect to GitHub",
            Width     = 180,
            Height    = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor    = Cursors.Hand,
            Location  = new Point(220, 140)
        };
        _ghConnectBtn.FlatAppearance.BorderSize = 0;
        _ghConnectBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(74, 109, 235);
        _ghConnectBtn.Click += async (_, _) => await TestGitHubConnectionAsync();

        connCard.Controls.AddRange(new Control[]
        {
            patLabel, _ghPatBox, eyeBtn, patHint, _ghStatusBadge!, _ghConnectBtn
        });
        stack.Controls.Add(connCard);

        // ── Step 2: Profile Card (hidden until connected) ───────────────────
        _ghProfileCard = new Panel
        {
            Width     = 740,
            BackColor = BgCard,
            Height    = 130,
            Margin    = new Padding(0, 0, 0, 16),
            Visible   = false
        };
        _ghProfileCard.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(49, 196, 141, 80), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, _ghProfileCard.Width - 1, _ghProfileCard.Height - 1);
        };

        var profHeader = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(10, 30, 22) };
        profHeader.Controls.Add(new Label
        {
            Text      = "Connected Account",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = AccentGreen,
            Location  = new Point(16, 10)
        });
        _ghProfileCard.Controls.Add(profHeader);

        // Avatar
        _ghAvatar = new PictureBox
        {
            Width    = 60,
            Height   = 60,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(16, 50),
            BackColor= BgField
        };
        MakeCirclePictureBox(_ghAvatar);

        // User info labels
        _ghUserName = new Label
        {
            Text      = "",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location  = new Point(92, 46)
        };
        _ghUserLogin = new Label
        {
            Text      = "",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9F),
            ForeColor = AccentBlue,
            Location  = new Point(92, 70)
        };
        _ghUserMeta = new Label
        {
            Text      = "",
            AutoSize  = true,
            Font      = new Font("Segoe UI", 8.5F),
            ForeColor = TextMuted,
            Location  = new Point(92, 92)
        };

        // Disconnect button
        var disconnectBtn = new Button
        {
            Text      = "Disconnect",
            Width     = 100,
            Height    = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 20, 20),
            ForeColor = AccentRed,
            Font      = new Font("Segoe UI", 9F),
            Cursor    = Cursors.Hand,
            Location  = new Point(624, 50)
        };
        disconnectBtn.FlatAppearance.BorderSize  = 1;
        disconnectBtn.FlatAppearance.BorderColor = AccentRed;
        disconnectBtn.Click += (_, _) =>
        {
            if (_ghPatBox != null) _ghPatBox.Text = "";
            UpdateConnectionBadge(false, null);
            if (_ghProfileCard != null) _ghProfileCard.Visible = false;
            if (_ghConnectBtn != null) _ghConnectBtn.Text = "🔗  Connect to GitHub";
        };

        _ghProfileCard.Controls.AddRange(new Control[]
        {
            _ghAvatar, _ghUserName, _ghUserLogin, _ghUserMeta, disconnectBtn
        });
        stack.Controls.Add(_ghProfileCard);

        // ── Sync settings ───────────────────────────────────────────────────
        AddCard(stack, "Sync Settings", new[]
        {
            MakeTextRow("Application Name",         "GitHubApp",   _config.Settings.GitHub.ApplicationName, "User-Agent string sent to GitHub API"),
            MakeToggleRow("Enable Sync",             "GitHubSync",        _config.Settings.GitHub.EnableSync,             "Enable automatic repository sync"),
            MakeNumberRow("Sync Interval (minutes)", "GitHubSyncMin",     _config.Settings.GitHub.SyncIntervalMinutes,    "How often to sync with GitHub"),
            MakeNumberRow("Max Commits Per Sync",    "GitHubMaxCommits",  _config.Settings.GitHub.MaxCommitsPerSync,      "Maximum commits fetched per sync"),
            MakeNumberRow("Max Issues Per Sync",     "GitHubMaxIssues",   _config.Settings.GitHub.MaxIssuesPerSync,       "Maximum issues fetched per sync"),
            MakeNumberRow("Cache Expiry (minutes)",  "GitHubCache",       _config.Settings.GitHub.CacheExpirationMinutes, "How long to cache GitHub API responses"),
            MakeTextRow("Default Branch",            "G_Branch",      _config.Settings.GitHub.DefaultBranch,          "Default branch to track (e.g. main)"),
        });

        // Auto-test if a PAT is already saved
        if (!string.IsNullOrWhiteSpace(_config.GetGitHubPat()))
            _ = TestGitHubConnectionAsync(silent: true);
    }

    private void UpdateConnectionBadge(bool connected, string? message)
    {
        if (_ghStatusBadge == null) return;
        if (connected)
        {
            _ghStatusBadge.Text      = $"● Connected  {message}";
            _ghStatusBadge.ForeColor = AccentGreen;
        }
        else
        {
            _ghStatusBadge.Text      = message != null ? $"○ {message}" : "○ Not connected";
            _ghStatusBadge.ForeColor = message != null && message.StartsWith("Error")
                ? AccentRed
                : Color.FromArgb(130, 150, 180);
        }
    }

    private static void MakeCirclePictureBox(PictureBox pb)
    {
        pb.Resize += (_, _) =>
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, pb.Width - 1, pb.Height - 1);
            pb.Region = new Region(path);
        };
    }

    private async Task TestGitHubConnectionAsync(bool silent = false)
    {
        var pat = _ghPatBox?.Text.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(pat))
        {
            if (!silent)
                UpdateConnectionBadge(false, "Error: token is empty");
            return;
        }

        // Cancel any previous in-flight call and start a new one
        _ghCts?.Cancel();
        _ghCts = new CancellationTokenSource();
        var ct = _ghCts.Token;

        // Update button state
        if (_ghConnectBtn != null)
        {
            _ghConnectBtn.Enabled = false;
            _ghConnectBtn.Text    = "Connecting…";
        }
        UpdateConnectionBadge(false, "Verifying token…");

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                _config.Settings.GitHub.ApplicationName ?? "Gradual");
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", pat);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

            // ── fetch user ────────────────────────────────────────────────
            var userResp = await http.GetAsync("https://api.github.com/user", ct);
            ct.ThrowIfCancellationRequested();
            if (!userResp.IsSuccessStatusCode)
            {
                var err = userResp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "Error: invalid or expired token"
                    : $"Error: GitHub returned {(int)userResp.StatusCode}";
                UpdateConnectionBadge(false, err);
                if (_ghConnectBtn != null)
                {
                    _ghConnectBtn.Enabled = true;
                    _ghConnectBtn.Text    = "🔗  Connect to GitHub";
                }
                return;
            }

            using var userDoc  = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync());
            var userRoot       = userDoc.RootElement;
            var login          = userRoot.GetProperty("login").GetString() ?? "";
            var name           = userRoot.TryGetProperty("name", out var nEl)  ? nEl.GetString() ?? login : login;
            var avatarUrl      = userRoot.TryGetProperty("avatar_url", out var aEl) ? aEl.GetString() ?? "" : "";
            var publicRepos    = userRoot.TryGetProperty("public_repos", out var rEl)  ? rEl.GetInt32() : 0;
            var followers      = userRoot.TryGetProperty("followers", out var fEl)     ? fEl.GetInt32() : 0;
            var following      = userRoot.TryGetProperty("following", out var flEl)    ? flEl.GetInt32() : 0;

            // ── fetch rate limit ──────────────────────────────────────────
            var rateResp  = await http.GetAsync("https://api.github.com/rate_limit", ct);
            ct.ThrowIfCancellationRequested();
            int rlLimit = 5000, rlRemaining = 5000;
            if (rateResp.IsSuccessStatusCode)
            {
                using var rateDoc = JsonDocument.Parse(await rateResp.Content.ReadAsStringAsync());
                var core = rateDoc.RootElement.GetProperty("rate");
                rlLimit     = core.GetProperty("limit").GetInt32();
                rlRemaining = core.GetProperty("remaining").GetInt32();
            }

            // ── update UI on UI thread ────────────────────────────────────
            if (!ct.IsCancellationRequested && IsHandleCreated)
                this.Invoke(() =>
                {
                    UpdateConnectionBadge(true, $"as  @{login}");

                    if (_ghConnectBtn != null)
                    {
                        _ghConnectBtn.Enabled = true;
                        _ghConnectBtn.Text    = "🔄  Re-test Connection";
                    }

                    // Profile card
                    if (_ghUserName  != null) _ghUserName.Text  = string.IsNullOrWhiteSpace(name) ? login : name;
                    if (_ghUserLogin != null) _ghUserLogin.Text = $"@{login}";
                    if (_ghUserMeta  != null)
                        _ghUserMeta.Text = $"📦 {publicRepos} repos  ·  👥 {followers} followers  ·  ➡ {following} following  ·  " +
                                           $"⚡ {rlRemaining}/{rlLimit} API calls left";

                    // Avatar (async image load)
                    if (_ghAvatar != null && !string.IsNullOrEmpty(avatarUrl))
                        _ = LoadAvatarAsync(_ghAvatar, avatarUrl);

                    if (_ghProfileCard != null)
                        _ghProfileCard.Visible = true;
                });
        }
        catch (OperationCanceledException)
        {
            // User navigated away — silently discard
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested && IsHandleCreated)
                this.Invoke(() =>
                {
                    UpdateConnectionBadge(false, $"Error: {ex.Message}");
                    if (_ghConnectBtn != null)
                    {
                        _ghConnectBtn.Enabled = true;
                        _ghConnectBtn.Text    = "🔗  Connect to GitHub";
                    }
                });
        }
    }

    private static async Task LoadAvatarAsync(PictureBox pb, string url)
    {
        try
        {
            using var http = new HttpClient();
            var bytes  = await http.GetByteArrayAsync(url);
            using var ms = new System.IO.MemoryStream(bytes);
            var img    = Image.FromStream(ms);
            pb.Invoke(() => pb.Image = img);
        }
        catch { /* avatar load failures are non-fatal */ }
    }


    private void BuildAnalyticsSection(FlowLayoutPanel stack)
    {
        AddSectionTitle(stack, "📊  Analytics", "Control how Gradual tracks usage and performance data locally.");
        AddCard(stack, "Tracking", new[]
        {
            MakeToggleRow("Enable Analytics",    "AnaEnabled",  _config.Settings.Analytics.Enabled,           "Enable local analytics tracking"),
            MakeToggleRow("Track User Actions",  "AnaActions",  _config.Settings.Analytics.TrackUserActions,  "Record button clicks and navigation events"),
            MakeToggleRow("Track Performance",   "AnaPerfM",    _config.Settings.Analytics.TrackPerformance,  "Record load times and operation durations"),
            MakeNumberRow("Session Timeout (min)","AnaTimeout", _config.Settings.Analytics.SessionTimeout,    "Minutes of inactivity before a new session starts"),
        });
    }

    // ── SECTION: Webhooks ─────────────────────────────────────────────────

    private void BuildWebhooksSection(FlowLayoutPanel stack)
    {
        AddSectionTitle(stack, "🔔  Webhooks", "Fire HTTP POST requests when project events occur.");
        AddCard(stack, "Global", new[]
        {
            MakeToggleRow("Enable Webhooks", "WebEnabled", _config.Settings.Webhooks.Enabled,
                          "Master switch — enables all webhook endpoints"),
        });

        var endpoints = _config.Settings.Webhooks.Endpoints;
        if (endpoints.Count == 0)
        {
            // Show a friendly empty-state message
            var hint = new Panel { Width = 740, Height = 48, BackColor = BgCard, Margin = new Padding(0, 0, 0, 16) };
            hint.Controls.Add(new Label
            {
                Text      = "No webhook endpoints configured. Add entries to the Endpoints array in appsettings.json.",
                AutoSize  = false,
                Width     = 720,
                Height    = 48,
                Font      = new Font("Segoe UI", 9F),
                ForeColor = TextMuted,
                Location  = new Point(12, 14)
            });
            stack.Controls.Add(hint);
        }
        else
        {
            foreach (var ep in endpoints)
            {
                var safeKey = ep.Name.Replace(" ", "");
                AddCard(stack, ep.Name, new[]
                {
                    MakeToggleRow("Enabled", $"Web{safeKey}On",  ep.Enabled,
                                  $"Fire this webhook on {ep.Name} events"),
                    MakeTextRow("URL",       $"Web{safeKey}Url", ep.Url,
                                  "Full URL that receives the HTTP POST payload"),
                });
            }
        }
    }

    // ── SECTION: Validation ───────────────────────────────────────────────

    private void BuildValidationSection(FlowLayoutPanel stack)
    {
        AddSectionTitle(stack, "📋  Validation", "Maximum allowed lengths and counts enforced when saving projects.");
        AddCard(stack, "Field Limits", new[]
        {
            MakeNumberRow("Max Project Name Length", "ValProjName",  _config.Settings.Validation.MaxProjectNameLength,
                          "Characters allowed in the project name field"),
            MakeNumberRow("Max Client Name Length",  "ValClient",    _config.Settings.Validation.MaxClientNameLength,
                          "Characters allowed in the client name field"),
            MakeNumberRow("Max Notes Length",        "ValNotes",     _config.Settings.Validation.MaxNotesLength,
                          "Characters allowed in the notes/description field"),
            MakeNumberRow("Max Folder Path Length",  "ValFolder",    _config.Settings.Validation.MaxFolderPathLength,
                          "Characters allowed in the folder path field"),
            MakeNumberRow("Max Phases",              "ValPhases",    _config.Settings.Validation.MaxPhases,
                          "Maximum number of phases per project"),
            MakeNumberRow("Max Phase Name Length",   "ValPhaseLen",  _config.Settings.Validation.MaxPhaseLength,
                          "Characters allowed in each phase name"),
            MakeNumberRow("Max Attachments",         "ValAttach",    _config.Settings.Validation.MaxAttachments,
                          "Maximum number of file attachments per project"),
        });
    }

    // ── UI building helpers ───────────────────────────────────────────────

    private void AddSectionTitle(FlowLayoutPanel stack, string title, string sub)
    {
        var p = new Panel { Width = 740, Height = 64, BackColor = BgContent, Margin = new Padding(0, 0, 0, 16) };
        p.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(0, 4) });
        p.Controls.Add(new Label { Text = sub,   AutoSize = true, Font = new Font("Segoe UI", 9.5F),               ForeColor = TextMuted,   Location = new Point(2, 40) });
        stack.Controls.Add(p);
    }

    private void AddCard(FlowLayoutPanel stack, string cardTitle, Control[] rows)
    {
        // Card container
        var card = new Panel
        {
            Width     = 740,
            BackColor = BgCard,
            Margin    = new Padding(0, 0, 0, 16),
            Padding   = new Padding(0)
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        // Card header
        var cardHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 38,
            BackColor = Color.FromArgb(12, 22, 40)
        };
        cardHeader.Controls.Add(new Label
        {
            Text      = cardTitle,
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(160, 185, 220),
            Location  = new Point(16, 10)
        });
        card.Controls.Add(cardHeader);

        int y = 38;
        foreach (var row in rows)
        {
            row.Location = new Point(0, y);
            card.Controls.Add(row);
            y += row.Height;
        }
        card.Height = y + 4;

        stack.Controls.Add(card);
    }

    private Panel MakeTextRow(string label, string key, string value, string hint)
    {
        var row = MakeBaseRow(label, hint, out int fieldX);
        var tb  = new TextBox
        {
            Text      = value,
            Width     = 340,
            Height    = 26,
            BackColor = BgField,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Font      = new Font("Segoe UI", 9.5F),
            Location  = new Point(fieldX, 12)
        };
        row.Controls.Add(tb);
        _fields[key] = tb;
        return row;
    }

    private Panel MakePasswordRow(string label, string key, string value, string hint)
    {
        var row = MakeBaseRow(label, hint, out int fieldX);
        var tb  = new TextBox
        {
            Text          = value,
            Width         = 340,
            Height        = 26,
            BackColor     = BgField,
            ForeColor     = TextPrimary,
            BorderStyle   = BorderStyle.FixedSingle,
            Font          = new Font("Segoe UI", 9.5F),
            PasswordChar  = '●',
            Location      = new Point(fieldX, 12)
        };
        // Reveal toggle
        var eye = new Button
        {
            Text      = "👁",
            Width     = 28,
            Height    = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = BgField,
            ForeColor = TextMuted,
            Cursor    = Cursors.Hand,
            Location  = new Point(fieldX + 344, 12)
        };
        eye.FlatAppearance.BorderSize = 0;
        eye.Click += (_, _) => tb.PasswordChar = tb.PasswordChar == '\0' ? '●' : '\0';
        row.Controls.Add(tb);
        row.Controls.Add(eye);
        _fields[key] = tb;
        return row;
    }

    private Panel MakeToggleRow(string label, string key, bool value, string hint)
    {
        var row = MakeBaseRow(label, hint, out int fieldX);
        var chk = new CheckBox
        {
            Checked   = value,
            Width     = 20,
            Height    = 20,
            BackColor = Color.Transparent,
            Location  = new Point(fieldX, 14)
        };
        row.Controls.Add(chk);
        _fields[key] = chk;
        return row;
    }

    private Panel MakeNumberRow(string label, string key, int value, string hint)
    {
        var row = MakeBaseRow(label, hint, out int fieldX);
        var num = new NumericUpDown
        {
            Minimum   = 0,
            Maximum   = 100000,
            Value     = Math.Max(0, Math.Min(100000, value)),
            Width     = 120,
            Height    = 26,
            BackColor = BgField,
            ForeColor = TextPrimary,
            Font      = new Font("Segoe UI", 9.5F),
            Location  = new Point(fieldX, 12)
        };
        row.Controls.Add(num);
        _fields[key] = num;
        return row;
    }

    private static Panel MakeBaseRow(string label, string hint, out int fieldX)
    {
        const int LabelW = 280;
        fieldX = LabelW + 16;

        var row = new Panel
        {
            Width     = 740,
            Height    = 50,
            BackColor = BgCard
        };
        row.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(20, 40, 68), 1);
            e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };

        row.Controls.Add(new Label
        {
            Text      = label,
            AutoSize  = false,
            Width     = LabelW - 8,
            Height    = 22,
            Font      = new Font("Segoe UI", 9.5F),
            ForeColor = TextPrimary,
            Location  = new Point(16, 8)
        });

        if (!string.IsNullOrEmpty(hint))
        {
            row.Controls.Add(new Label
            {
                Text      = hint,
                AutoSize  = false,
                Width     = LabelW - 8,
                Height    = 16,
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = TextMuted,
                Location  = new Point(16, 28)
            });
        }
        return row;
    }

    private void AddActionButtons(FlowLayoutPanel stack)
    {
        var bar = new Panel { Width = 740, Height = 56, BackColor = BgContent, Margin = new Padding(0, 8, 0, 0) };

        var saveBtn = new Button
        {
            Text      = "💾  Save Changes",
            Width     = 160,
            Height    = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor    = Cursors.Hand,
            Location  = new Point(0, 9)
        };
        saveBtn.FlatAppearance.BorderSize = 0;
        saveBtn.Click += SaveSettings_Click;
        bar.Controls.Add(saveBtn);

        var resetBtn = new Button
        {
            Text      = "↺  Discard",
            Width     = 110,
            Height    = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 50, 80),
            ForeColor = Color.FromArgb(160, 185, 220),
            Font      = new Font("Segoe UI", 10F),
            Cursor    = Cursors.Hand,
            Location  = new Point(170, 9)
        };
        resetBtn.FlatAppearance.BorderSize  = 0;
        resetBtn.FlatAppearance.BorderColor = Border;
        resetBtn.Click += (_, _) => ShowSection(_currentSection);
        bar.Controls.Add(resetBtn);

        stack.Controls.Add(bar);
    }

    // ── Save logic ────────────────────────────────────────────────────────

    private void SaveSettings_Click(object? sender, EventArgs e)
    {
        try
        {
            // Read existing JSON
            var json    = File.ReadAllText(_settingsFilePath);
            var doc     = JsonDocument.Parse(json);
            var root    = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
            var mutable = DeepCopy(root);

            // Apply field values into the mutable dict based on current section
            ApplyFields(mutable);

            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(mutable, opts));

            // Hot-reload so the running ConfigurationService sees the new values immediately
            _config.Reload();

            ShowBanner("✓  Settings saved.", AccentGreen);
        }
        catch (Exception ex)
        {
            ShowBanner($"✕  Failed to save: {ex.Message}", AccentRed);
        }
    }

    private void ApplyFields(Dictionary<string, object?> root)
    {
        T? GetField<T>(string key) where T : Control
            => _fields.TryGetValue(key, out var c) ? c as T : null;

        string Txt(string k, string fallback) => GetField<TextBox>(k)?.Text ?? fallback;
        bool   Chk(string k, bool fallback)   => GetField<CheckBox>(k)?.Checked ?? fallback;
        int    Num(string k, int fallback)     => (int)(GetField<NumericUpDown>(k)?.Value ?? fallback);

        switch (_currentSection)
        {
            case "General":
                SetPath(root, "ApplicationSettings", "ApplicationName",   Txt("AppName",      "Gradual"));
                SetPath(root, "ApplicationSettings", "DataDirectory",      Txt("DataDir",      "data"));
                SetPath(root, "ApplicationSettings", "BackupDirectory",    Txt("BackupDir",    "backups"));
                SetPath(root, "ApplicationSettings", "EnableAutoBackup",   Chk("AutoBackup",   true));
                SetPath(root, "ApplicationSettings", "BackupIntervalMinutes", Num("BackupInterval", 30));
                SetPath(root, "ApplicationSettings", "MaxBackupFiles",     Num("MaxBackups",   10));
                break;

            case "GitHub":
                SetPath(root, "GitHub", "PersonalAccessToken",    Txt("GitHubPAT",        ""));
                SetPath(root, "GitHub", "ApplicationName",         Txt("GitHubApp",        "Gradual"));
                SetPath(root, "GitHub", "EnableSync",              Chk("GitHubSync",       false));
                SetPath(root, "GitHub", "SyncIntervalMinutes",     Num("GitHubSyncMin",    30));
                SetPath(root, "GitHub", "MaxCommitsPerSync",       Num("GitHubMaxCommits", 100));
                SetPath(root, "GitHub", "MaxIssuesPerSync",        Num("GitHubMaxIssues",  50));
                SetPath(root, "GitHub", "CacheExpirationMinutes",  Num("GitHubCache",      15));
                SetPath(root, "GitHub", "DefaultBranch",           Txt("G_Branch",     "main"));
                break;

            case "Analytics":
                SetPath(root, "Analytics", "Enabled",          Chk("AnaEnabled", true));
                SetPath(root, "Analytics", "TrackUserActions", Chk("AnaActions",  true));
                SetPath(root, "Analytics", "TrackPerformance", Chk("AnaPerfM",    true));
                SetPath(root, "Analytics", "SessionTimeout",   Num("AnaTimeout",  30));
                break;

            case "Webhooks":
                SetPath(root, "Webhooks", "Enabled", Chk("WebEnabled", false));

                // Rebuild the full Endpoints array so per-endpoint Url + Enabled are saved.
                var endpoints = _config.Settings.Webhooks.Endpoints;
                var updatedEndpoints = new List<object?>();
                foreach (var ep in endpoints)
                {
                    var k = ep.Name.Replace(" ", "");
                    updatedEndpoints.Add(new Dictionary<string, object?>
                    {
                        ["Name"]    = ep.Name,
                        ["Url"]     = Txt($"Web{k}Url", ep.Url),
                        ["Enabled"] = Chk($"Web{k}On",  ep.Enabled),
                    });
                }

                // Write the endpoints array back into the Webhooks section
                if (root.TryGetValue("Webhooks", out var webObj)
                    && webObj is Dictionary<string, object?> webDict)
                {
                    webDict["Endpoints"] = updatedEndpoints;
                }
                break;

            case "Validation":
                SetPath(root, "Validation", "MaxProjectNameLength", Num("ValProjName", 200));
                SetPath(root, "Validation", "MaxClientNameLength",  Num("ValClient",   200));
                SetPath(root, "Validation", "MaxNotesLength",       Num("ValNotes",    5000));
                SetPath(root, "Validation", "MaxFolderPathLength",  Num("ValFolder",   500));
                SetPath(root, "Validation", "MaxPhases",            Num("ValPhases",   50));
                SetPath(root, "Validation", "MaxPhaseLength",       Num("ValPhaseLen", 100));
                SetPath(root, "Validation", "MaxAttachments",       Num("ValAttach",   100));

                // Propagate live limits into the registered validator so they
                // take effect immediately without requiring a restart.
                try
                {
                    var validator = Infrastructure.ServiceContainer
                        .GetService<Validation.IValidator<Models.DTOs.ProjectDto>>()
                        as Validation.ProjectValidator;
                    validator?.UpdateLimits(
                        Num("ValProjName", 200),
                        Num("ValClient",   200),
                        Num("ValNotes",    5000),
                        Num("ValFolder",   500),
                        Num("ValPhases",   50),
                        Num("ValPhaseLen", 100),
                        Num("ValAttach",   100));
                }
                catch { /* validator not resolvable — non-fatal */ }
                break;
        }
    }

    // ── JSON helpers ─────────────────────────────────────────────────────

    private static Dictionary<string, object?> DeepCopy(Dictionary<string, JsonElement> source)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kv in source)
            result[kv.Key] = ElementToObject(kv.Value);
        return result;
    }

    private static object? ElementToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object  => el.EnumerateObject().ToDictionary(p => p.Name, p => ElementToObject(p.Value)),
        JsonValueKind.Array   => el.EnumerateArray().Select(ElementToObject).ToList(),
        JsonValueKind.String  => el.GetString(),
        JsonValueKind.Number  => el.TryGetInt32(out var i) ? (object?)i : el.GetDouble(),
        JsonValueKind.True    => true,
        JsonValueKind.False   => false,
        _                     => null
    };

    private static void SetPath(Dictionary<string, object?> root, string section, string key, object? value)
    {
        if (!root.TryGetValue(section, out var secObj) || secObj is not Dictionary<string, object?> secDict)
        {
            secDict = new Dictionary<string, object?>();
            root[section] = secDict;
        }
        secDict[key] = value;
    }

    // ── Banner notification ───────────────────────────────────────────────

    private void ShowBanner(string message, Color color)
    {
        // Remove any previous banner
        foreach (var old in _contentArea.Controls.OfType<Panel>()
            .Where(p => p.Tag?.ToString() == "banner").ToList())
            _contentArea.Controls.Remove(old);

        var banner = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 42,
            BackColor = Color.FromArgb(20, color.R / 6, color.G / 6, color.B / 6),
            Tag       = "banner"
        };
        banner.Paint += (_, e) =>
        {
            using var pen = new Pen(color, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, banner.Width - 1, banner.Height - 1);
        };
        banner.Controls.Add(new Label
        {
            Text      = message,
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = color,
            Location  = new Point(14, 11)
        });
        _contentArea.Controls.Add(banner);

        // Auto-dismiss after 4 seconds
        var timer = new System.Windows.Forms.Timer { Interval = 4000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (banner.IsHandleCreated)
                banner.BeginInvoke(() => _contentArea.Controls.Remove(banner));
        };
        timer.Start();
    }
}
