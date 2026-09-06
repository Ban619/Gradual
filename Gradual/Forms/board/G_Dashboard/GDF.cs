using System.Diagnostics;
using Gradual.Models.GitHub;
using Gradual.Services.GitHub;
using Gradual.Repositories.GitHub;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Data.Common;

namespace Gradual.Forms;

/// <summary>
/// GitHub Dashboard - Standalone form matching GitHub's UI style
/// Displays linked repositories in a clean, dark-themed interface
/// </summary>
public partial class GDF : Form
{
    private readonly IGitHubService? _githubService;
    private readonly IG_ReposStore? _repositoryStore;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    
    // UI Components
    private Panel _headerPanel = null!;
    private PictureBox _logoBox = null!;
    private Label _dashboardTitle = null!;
    private TextBox _searchBox = null!;
    private Panel _contentPanel = null!;
    private Label _topReposLabel = null!;
    private FlowLayoutPanel _repositoriesFlow = null!;
    private LinkLabel _showMoreLink = null!;
    
    // Data
    private List<G_Repos> _repositories = new();
    private List<G_Repos> _filteredRepositories = new();
    private const int InitialDisplayCount = 7;
    private bool _showingAll = false;

    // Colors matching GitHub dark theme
    private static readonly Color BackgroundColor = Color.FromArgb(13, 17, 23);
    private static readonly Color HeaderColor = Color.FromArgb(22, 27, 34);
    private static readonly Color RepoItemColor = Color.FromArgb(22, 27, 34);
    private static readonly Color RepoItemHoverColor = Color.FromArgb(32, 37, 44);
    private static readonly Color BorderColor = Color.FromArgb(48, 54, 61);
    private static readonly Color TextColor = Color.FromArgb(201, 209, 217);
    private static readonly Color TextSecondaryColor = Color.FromArgb(139, 148, 158);
    private static readonly Color LinkColor = Color.FromArgb(88, 166, 255);
    private static readonly Color SearchBoxColor = Color.FromArgb(13, 17, 23);

    public GDF(
        IGitHubService? githubService, 
        IG_ReposStore? repositoryStore,
        ILogger logger, 
        IConfiguration configuration)
    {
        _githubService = githubService;
        _repositoryStore = repositoryStore;
        _logger = logger;
        _configuration = configuration;
        
        InitializeComponents();
        LoadRepositories();
    }

    private void InitializeComponents()
    {
        // Form setup
        this.Text = "GitHub Dashboard - Gradual";
        this.Size = new Size(900, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = BackgroundColor;
        this.Font = new Font("Segoe UI", 9F);
        this.MinimumSize = new Size(700, 500);
        
        // Header Panel
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = HeaderColor,
            Padding = new Padding(20, 0, 20, 0)
        };
        
        // GitHub Logo (using text for now, can be replaced with image)
        _logoBox = new PictureBox
        {
            Size = new Size(40, 40),
            Location = new Point(20, 12),
            BackColor = Color.White,
            SizeMode = PictureBoxSizeMode.CenterImage
        };
        // Draw a simple GitHub logo representation
        DrawGitHubLogo();
        
        // Dashboard Title
        _dashboardTitle = new Label
        {
            Text = "Dashboard",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = TextColor,
            AutoSize = true,
            Location = new Point(70, 18)
        };
        
        _headerPanel.Controls.Add(_logoBox);
        _headerPanel.Controls.Add(_dashboardTitle);
        
        // Content Panel
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackgroundColor,
            Padding = new Padding(30),
            AutoScroll = true
        };
        
        // Top repositories section
        _topReposLabel = new Label
        {
            Text = "Top repositories",
            Font = new Font("Segoe UI", 16F, FontStyle.Regular),
            ForeColor = TextColor,
            AutoSize = true,
            Location = new Point(0, 20)
        };
        
        // Search box
        _searchBox = new TextBox
        {
            Size = new Size(400, 36),
            Location = new Point(0, 60),
            Font = new Font("Segoe UI", 13F),
            BackColor = SearchBoxColor,
            ForeColor = TextSecondaryColor,
            BorderStyle = BorderStyle.FixedSingle,
            Text = "Find a repository..."
        };
        _searchBox.GotFocus += SearchBox_GotFocus;
        _searchBox.LostFocus += SearchBox_LostFocus;
        _searchBox.TextChanged += SearchBox_TextChanged;
        
        // Repositories FlowLayoutPanel
        _repositoriesFlow = new FlowLayoutPanel
        {
            Location = new Point(0, 110),
            Width = 820,
            Height = 450,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Color.Transparent
        };
        
        // Show more link
        _showMoreLink = new LinkLabel
        {
            Text = "Show more",
            Font = new Font("Segoe UI", 13F),
            LinkColor = TextSecondaryColor,
            ActiveLinkColor = TextColor,
            AutoSize = true,
            Location = new Point(10, 570),
            Cursor = Cursors.Hand
        };
        _showMoreLink.LinkBehavior = LinkBehavior.HoverUnderline;
        _showMoreLink.Click += ShowMoreLink_Click;
        
        _contentPanel.Controls.Add(_topReposLabel);
        _contentPanel.Controls.Add(_searchBox);
        _contentPanel.Controls.Add(_repositoriesFlow);
        _contentPanel.Controls.Add(_showMoreLink);
        
        this.Controls.Add(_contentPanel);
        this.Controls.Add(_headerPanel);
    }

    private void DrawGitHubLogo()
    {
        // Create a simple GitHub "octocat" representation using a bitmap
        var bmp = new Bitmap(40, 40);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            
            // Draw GitHub logo (simplified circle with "G")
            g.FillEllipse(Brushes.Black, 2, 2, 36, 36);
            using (var font = new Font("Segoe UI", 16F, FontStyle.Bold))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("G", font, Brushes.White, new RectangleF(0, 0, 40, 40), sf);
            }
        }
        _logoBox.Image = bmp;
    }

    private async void LoadRepositories()
    {
        try
        {
            if (_repositoryStore == null)
            {
                _logger.Warning("G_ReposStore is not available");
                ShowNoRepositoriesMessage();
                return;
            }
            
            // Get all repositories from database
            _repositories = await _repositoryStore.GetAllRepositoriesAsync();
            
            if (!_repositories.Any())
            {
                ShowNoRepositoriesMessage();
                return;
            }
            
            // Sort by last synced (most recent first) or stars
            _repositories = _repositories
                .OrderByDescending(r => r.Stars)
                .ThenByDescending(r => r.LastSyncedAt)
                .ToList();
            
            _filteredRepositories = _repositories;
            DisplayRepositories(false);
            
            _logger.Information("Loaded {Count} GitHub repositories", _repositories.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load GitHub repositories");
            MessageBox.Show($"Failed to load repositories: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DisplayRepositories(bool showAll)
    {
        _repositoriesFlow.Controls.Clear();
        
        var displayCount = showAll ? _filteredRepositories.Count : Math.Min(InitialDisplayCount, _filteredRepositories.Count);
        
        for (int i = 0; i < displayCount; i++)
        {
            var repo = _filteredRepositories[i];
            var repoItem = CreateRepositoryItem(repo);
            _repositoriesFlow.Controls.Add(repoItem);
        }
        
        // Show/hide "Show more" link
        _showMoreLink.Visible = _filteredRepositories.Count > InitialDisplayCount;
        _showMoreLink.Text = showAll ? "Show less" : "Show more";
    }

    private Panel CreateRepositoryItem(G_Repos repo)
    {
        var panel = new Panel
        {
            Width = 800,
            Height = 50,
            BackColor = RepoItemColor,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 1)
        };
        
        // Repository icon
        var iconLabel = new Label
        {
            Text = "📦",
            Font = new Font("Segoe UI", 16F),
            ForeColor = TextSecondaryColor,
            Size = new Size(40, 40),
            Location = new Point(10, 5),
            TextAlign = ContentAlignment.MiddleCenter
        };
        
        // Repository name
        var nameLabel = new Label
        {
            Text = $"{repo.Owner}/{repo.Name}",
            Font = new Font("Segoe UI", 14F, FontStyle.Regular),
            ForeColor = LinkColor,
            AutoSize = true,
            Location = new Point(55, 14),
            Cursor = Cursors.Hand
        };
        nameLabel.Click += (s, e) => OpenRepository(repo);
        
        // Repository stats (stars, language, etc.)
        var statsText = BuildStatsText(repo);
        var statsLabel = new Label
        {
            Text = statsText,
            Font = new Font("Segoe UI", 11F),
            ForeColor = TextSecondaryColor,
            AutoSize = true,
            Location = new Point(nameLabel.Right + 20, 16)
        };
        
        panel.Controls.Add(iconLabel);
        panel.Controls.Add(nameLabel);
        panel.Controls.Add(statsLabel);
        
        // Hover effects
        panel.MouseEnter += (s, e) => panel.BackColor = RepoItemHoverColor;
        panel.MouseLeave += (s, e) => panel.BackColor = RepoItemColor;
        panel.Click += (s, e) => OpenRepository(repo);
        
        return panel;
    }

    private string BuildStatsText(G_Repos repo)
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(repo.Language))
        {
            parts.Add($"🔵 {repo.Language}");
        }
        
        if (repo.Stars > 0)
        {
            parts.Add($"⭐ {repo.Stars}");
        }
        
        if (repo.Forks > 0)
        {
            parts.Add($"🍴 {repo.Forks}");
        }
        
        if (repo.LastSyncedAt != default)
        {
            var timeAgo = GetTimeAgo(repo.LastSyncedAt);
            parts.Add($"Updated {timeAgo}");
        }
        
        return string.Join("  •  ", parts);
    }

    private string GetTimeAgo(DateTime dateTime)
    {
        var span = DateTime.Now - dateTime;
        
        if (span.TotalMinutes < 1)
            return "just now";
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes} minutes ago";
        if (span.TotalHours < 24)
            return $"{(int)span.TotalHours} hours ago";
        if (span.TotalDays < 30)
            return $"{(int)span.TotalDays} days ago";
        if (span.TotalDays < 365)
            return $"{(int)(span.TotalDays / 30)} months ago";
        
        return $"{(int)(span.TotalDays / 365)} years ago";
    }

    private void OpenRepository(G_Repos repo)
    {
        try
        {
            // Open repository details form or navigate to GitHub
            if (!string.IsNullOrEmpty(repo.HtmlUrl))
            {
                var result = MessageBox.Show(
                    $"Do you want to view {repo.Owner}/{repo.Name} on GitHub?",
                    "Open Repository",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(repo.HtmlUrl) { UseShellExecute = true });
                    _logger.Information("Opened GitHub repository: {Url}", repo.HtmlUrl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open GitHub repository: {Repo}", repo.FullName);
            MessageBox.Show($"Failed to open repository: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowNoRepositoriesMessage()
    {
        _repositoriesFlow.Controls.Clear();
        
        var messagePanel = new Panel
        {
            Width = 800,
            Height = 200,
            BackColor = Color.Transparent
        };
        
        var messageLabel = new Label
        {
            Text = "No repositories linked yet",
            Font = new Font("Segoe UI", 16F),
            ForeColor = TextSecondaryColor,
            AutoSize = true,
            Location = new Point(250, 50)
        };
        
        var instructionLabel = new Label
        {
            Text = "Link your first GitHub repository to get started",
            Font = new Font("Segoe UI", 12F),
            ForeColor = TextSecondaryColor,
            AutoSize = true,
            Location = new Point(200, 90)
        };
        
        messagePanel.Controls.Add(messageLabel);
        messagePanel.Controls.Add(instructionLabel);
        _repositoriesFlow.Controls.Add(messagePanel);
        
        _showMoreLink.Visible = false;
    }

    private void SearchBox_GotFocus(object? sender, EventArgs e)
    {
        if (_searchBox.Text == "Find a repository...")
        {
            _searchBox.Text = "";
            _searchBox.ForeColor = TextColor;
        }
    }

    private void SearchBox_LostFocus(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_searchBox.Text))
        {
            _searchBox.Text = "Find a repository...";
            _searchBox.ForeColor = TextSecondaryColor;
        }
    }

    private void SearchBox_TextChanged(object? sender, EventArgs e)
    {
        if (_searchBox.Text == "Find a repository...")
            return;
        
        var searchTerm = _searchBox.Text.Trim().ToLowerInvariant();
        
        if (string.IsNullOrEmpty(searchTerm))
        {
            _filteredRepositories = _repositories;
        }
        else
        {
            _filteredRepositories = _repositories
                .Where(r => 
                    r.Name.ToLowerInvariant().Contains(searchTerm) ||
                    r.Owner.ToLowerInvariant().Contains(searchTerm) ||
                    (r.Description?.ToLowerInvariant().Contains(searchTerm) ?? false))
                .ToList();
        }
        
        _showingAll = false;
        DisplayRepositories(false);
    }

    private void ShowMoreLink_Click(object? sender, EventArgs e)
    {
        _showingAll = !_showingAll;
        DisplayRepositories(_showingAll);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        _logger.Information("GitHub Dashboard closed");
    }
}
