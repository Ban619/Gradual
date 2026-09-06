using Gradual.Controls;
using Gradual.Models.GitHub;
using Gradual.Services.GitHub;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Diagnostics;

namespace Gradual.Forms;

/// <summary>
/// GitHub integration panel for displaying repository status, commits, and issues
/// </summary>
public class GitHubIntegrationPanel : UserControl
{
    private readonly IGitHubService? _githubService;
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    
    // UI Components
    private RoundedPanel _mainPanel = null!;
    private Label _titleLabel = null!;
    private Button _linkRepoButton = null!;
    private Button _refreshButton = null!;
    private Label _syncStatusLabel = null!;
    
    // Repository Info Section
    private RoundedPanel _repoInfoPanel = null!;
    private Label _repoNameLabel = null!;
    private Label _repoStatsLabel = null!;
    private LinkLabel _repoLinkLabel = null!;
    
    // Commits Section
    private RoundedPanel _commitsPanel = null!;
    private FlowLayoutPanel _commitsFlow = null!;
    
    // Issues Section
    private RoundedPanel _issuesPanel = null!;
    private Label _issuesCountLabel = null!;
    
    // Current repository
    private G_Repos? _currentRepository;

    public GitHubIntegrationPanel(IGitHubService? githubService, ILogger logger, IConfiguration configuration)
    {
        _githubService = githubService;
        _logger = logger;
        _configuration = configuration;
        
        InitializeComponents();
        ApplyTheme();
        CheckGitHubConnection();
    }

    private void InitializeComponents()
    {
        // Main panel setup
        this.Dock = DockStyle.Fill;
        this.AutoScroll = true;
        this.BackColor = Color.FromArgb(18, 27, 45);
        
        _mainPanel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(11, 23, 40),
            BorderColor = Color.FromArgb(34, 54, 82),
            Radius = 15,
            Padding = new Padding(20),
            AutoScroll = true
        };
        
        // Header section
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = Color.Transparent
        };
        
        _titleLabel = new Label
        {
            Text = "🐙 GitHub Integration",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(0, 0)
        };
        
        _linkRepoButton = new Button
        {
            Text = "Link Repository",
            Location = new Point(0, 40),
            Size = new Size(140, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(88, 166, 255),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _linkRepoButton.FlatAppearance.BorderSize = 0;
        _linkRepoButton.Click += LinkRepoButton_Click;
        
        _refreshButton = new Button
        {
            Text = "🔄 Refresh",
            Location = new Point(150, 40),
            Size = new Size(100, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(52, 71, 103),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand
        };
        _refreshButton.FlatAppearance.BorderSize = 0;
        _refreshButton.Click += RefreshButton_Click;
        
        _syncStatusLabel = new Label
        {
            Text = "● Not connected",
            Location = new Point(260, 46),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(150, 160, 180)
        };
        
        headerPanel.Controls.AddRange(new Control[] 
        { 
            _titleLabel, 
            _linkRepoButton, 
            _refreshButton, 
            _syncStatusLabel 
        });
        
        // Repository Info Panel
        _repoInfoPanel = CreateRepoInfoPanel();
        _repoInfoPanel.Dock = DockStyle.Top;
        _repoInfoPanel.Visible = false;
        
        // Commits Panel
        _commitsPanel = CreateCommitsPanel();
        _commitsPanel.Dock = DockStyle.Top;
        _commitsPanel.Visible = false;
        
        // Issues Panel
        _issuesPanel = CreateIssuesPanel();
        _issuesPanel.Dock = DockStyle.Top;
        _issuesPanel.Visible = false;
        
        _mainPanel.Controls.Add(_issuesPanel);
        _mainPanel.Controls.Add(_commitsPanel);
        _mainPanel.Controls.Add(_repoInfoPanel);
        _mainPanel.Controls.Add(headerPanel);
        
        this.Controls.Add(_mainPanel);
    }

    private RoundedPanel CreateRepoInfoPanel()
    {
        var panel = new RoundedPanel
        {
            Height = 120,
            BackColor = Color.FromArgb(20, 33, 55),
            BorderColor = Color.FromArgb(44, 64, 92),
            Radius = 12,
            Padding = new Padding(15),
            Margin = new Padding(0, 10, 0, 0)
        };
        
        _repoNameLabel = new Label
        {
            Text = "Repository Name",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(15, 15)
        };
        
        _repoStatsLabel = new Label
        {
            Text = "⭐ 0 Stars • 🍴 0 Forks • 👀 0 Watchers",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(180, 190, 210),
            AutoSize = true,
            Location = new Point(15, 45)
        };
        
        _repoLinkLabel = new LinkLabel
        {
            Text = "View on GitHub →",
            Font = new Font("Segoe UI", 9F),
            LinkColor = Color.FromArgb(88, 166, 255),
            AutoSize = true,
            Location = new Point(15, 75),
            Cursor = Cursors.Hand
        };
        _repoLinkLabel.Click += RepoLinkLabel_Click;
        
        panel.Controls.AddRange(new Control[] 
        { 
            _repoNameLabel, 
            _repoStatsLabel, 
            _repoLinkLabel 
        });
        
        return panel;
    }

    private RoundedPanel CreateCommitsPanel()
    {
        var panel = new RoundedPanel
        {
            Height = 300,
            BackColor = Color.FromArgb(20, 33, 55),
            BorderColor = Color.FromArgb(44, 64, 92),
            Radius = 12,
            Padding = new Padding(15),
            Margin = new Padding(0, 10, 0, 0),
            AutoScroll = true
        };
        
        var titleLabel = new Label
        {
            Text = "📝 Recent Commits",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(15, 15)
        };
        
        _commitsFlow = new FlowLayoutPanel
        {
            Location = new Point(15, 45),
            Width = panel.Width - 40,
            Height = panel.Height - 60,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent
        };
        
        panel.Controls.AddRange(new Control[] { titleLabel, _commitsFlow });
        
        return panel;
    }

    private RoundedPanel CreateIssuesPanel()
    {
        var panel = new RoundedPanel
        {
            Height = 100,
            BackColor = Color.FromArgb(20, 33, 55),
            BorderColor = Color.FromArgb(44, 64, 92),
            Radius = 12,
            Padding = new Padding(15),
            Margin = new Padding(0, 10, 0, 0)
        };
        
        var titleLabel = new Label
        {
            Text = "🐛 Issues & Pull Requests",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(15, 15)
        };
        
        _issuesCountLabel = new Label
        {
            Text = "0 open issues • 0 open PRs",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(180, 190, 210),
            AutoSize = true,
            Location = new Point(15, 50)
        };
        
        panel.Controls.AddRange(new Control[] { titleLabel, _issuesCountLabel });
        
        return panel;
    }

    private async void CheckGitHubConnection()
    {
        if (_githubService == null)
        {
            UpdateSyncStatus("GitHub service not available", false);
            return;
        }
        
        var (success, message) = await _githubService.TestConnectionAsync();
        UpdateSyncStatus(message, success);
    }

    private void UpdateSyncStatus(string message, bool connected)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateSyncStatus(message, connected));
            return;
        }
        
        _syncStatusLabel.Text = connected ? $"● {message}" : $"○ {message}";
        _syncStatusLabel.ForeColor = connected 
            ? Color.FromArgb(76, 217, 100) 
            : Color.FromArgb(255, 107, 107);
    }

    public async Task LoadRepositoryAsync(string owner, string repoName)
    {
        if (_githubService == null)
        {
            MessageBox.Show("GitHub service is not available.", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        
        try
        {
            UpdateSyncStatus("Loading repository...", false);
            
            // Get repository info
            _currentRepository = await _githubService.GetRepositoryInfoAsync(owner, repoName);
            
            if (_currentRepository == null)
            {
                MessageBox.Show($"Repository {owner}/{repoName} not found.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                UpdateSyncStatus("Repository not found", false);
                return;
            }
            
            // Update UI
            DisplayRepositoryInfo();
            
            // Load commits
            var commits = await _githubService.GetCommitsAsync(owner, repoName, 10);
            DisplayCommits(commits);
            
            // Load issues
            var issues = await _githubService.GetIssuesAsync(owner, repoName, "open");
            var prs = await _githubService.GetPullRequestsAsync(owner, repoName, "open");
            DisplayIssues(issues.Count, prs.Count);
            
            UpdateSyncStatus($"Connected to {owner}/{repoName}", true);
            
            _logger.Information("Loaded GitHub repository: {Owner}/{Repo}", owner, repoName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load GitHub repository: {Owner}/{Repo}", owner, repoName);
            MessageBox.Show($"Failed to load repository: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateSyncStatus("Load failed", false);
        }
    }

    private void DisplayRepositoryInfo()
    {
        if (_currentRepository == null) return;
        
        _repoNameLabel.Text = $"{_currentRepository.Owner}/{_currentRepository.Name}";
        _repoStatsLabel.Text = $"⭐ {_currentRepository.Stars} Stars • " +
                               $"🍴 {_currentRepository.Forks} Forks • " +
                               $"👀 {_currentRepository.Watchers} Watchers";
        _repoLinkLabel.Tag = _currentRepository.HtmlUrl;
        
        _repoInfoPanel.Visible = true;
    }

    private void DisplayCommits(List<G_Commit> commits)
    {
        _commitsFlow.Controls.Clear();
        
        foreach (var commit in commits.Take(10))
        {
            var commitPanel = CreateCommitCard(commit);
            _commitsFlow.Controls.Add(commitPanel);
        }
        
        _commitsPanel.Visible = commits.Any();
    }

    private Panel CreateCommitCard(G_Commit commit)
    {
        var panel = new Panel
        {
            Width = _commitsFlow.Width - 10,
            Height = 70,
            BackColor = Color.FromArgb(28, 41, 63),
            Margin = new Padding(0, 0, 0, 8)
        };
        
        var messageLabel = new Label
        {
            Text = commit.Message.Length > 60 
                ? commit.Message.Substring(0, 60) + "..." 
                : commit.Message,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = false,
            Size = new Size(panel.Width - 20, 20),
            Location = new Point(10, 10)
        };
        
        var authorLabel = new Label
        {
            Text = $"👤 {commit.AuthorName} • {commit.CommittedAt:MMM dd, yyyy}",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(150, 160, 180),
            AutoSize = true,
            Location = new Point(10, 35)
        };
        
        var shaLabel = new Label
        {
            Text = commit.Sha.Substring(0, 7),
            Font = new Font("Consolas", 8F),
            ForeColor = Color.FromArgb(88, 166, 255),
            AutoSize = true,
            Location = new Point(10, 52)
        };
        
        panel.Controls.AddRange(new Control[] { messageLabel, authorLabel, shaLabel });
        panel.Cursor = Cursors.Hand;
        panel.Tag = commit.HtmlUrl;
        panel.Click += (s, e) => 
        {
            if (panel.Tag is string url && !string.IsNullOrEmpty(url))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        };
        
        return panel;
    }

    private void DisplayIssues(int issuesCount, int prsCount)
    {
        _issuesCountLabel.Text = $"{issuesCount} open issues • {prsCount} open PRs";
        _issuesPanel.Visible = true;
    }

    private async void LinkRepoButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new GitHubRepoLinkDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            await LoadRepositoryAsync(dialog.Owner, dialog.RepoName);
        }
    }

    private async void RefreshButton_Click(object? sender, EventArgs e)
    {
        if (_currentRepository != null)
        {
            await LoadRepositoryAsync(_currentRepository.Owner, _currentRepository.Name);
        }
    }

    private void RepoLinkLabel_Click(object? sender, EventArgs e)
    {
        if (_repoLinkLabel.Tag is string url && !string.IsNullOrEmpty(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void ApplyTheme()
    {
        // Theme is applied in component initialization
        // Can be enhanced for light/dark mode toggle
    }
}

/// <summary>
/// Dialog for linking a GitHub repository
/// </summary>
public class GitHubRepoLinkDialog : Form
{
    private TextBox _ownerTextBox = null!;
    private TextBox _repoNameTextBox = null!;
    private Button _okButton = null!;
    private Button _cancelButton = null!;
    
    public new string Owner => _ownerTextBox.Text.Trim();
    public string RepoName => _repoNameTextBox.Text.Trim();

    public GitHubRepoLinkDialog()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        this.Text = "Link GitHub Repository";
        this.Size = new Size(400, 220);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(18, 27, 45);
        
        var titleLabel = new Label
        {
            Text = "Enter Repository Information",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 20),
            AutoSize = true
        };
        
        var ownerLabel = new Label
        {
            Text = "Owner:",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(200, 210, 230),
            Location = new Point(20, 60),
            AutoSize = true
        };
        
        _ownerTextBox = new TextBox
        {
            Location = new Point(110, 58),
            Size = new Size(250, 25),
            Font = new Font("Segoe UI", 10F),
            BackColor = Color.FromArgb(28, 41, 63),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        
        var repoLabel = new Label
        {
            Text = "Repository:",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(200, 210, 230),
            Location = new Point(20, 100),
            AutoSize = true
        };
        
        _repoNameTextBox = new TextBox
        {
            Location = new Point(110, 98),
            Size = new Size(250, 25),
            Font = new Font("Segoe UI", 10F),
            BackColor = Color.FromArgb(28, 41, 63),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        
        _okButton = new Button
        {
            Text = "Link",
            Location = new Point(190, 140),
            Size = new Size(80, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(88, 166, 255),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            DialogResult = DialogResult.OK
        };
        _okButton.FlatAppearance.BorderSize = 0;
        
        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(280, 140),
            Size = new Size(80, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(52, 71, 103),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F),
            DialogResult = DialogResult.Cancel
        };
        _cancelButton.FlatAppearance.BorderSize = 0;
        
        this.Controls.AddRange(new Control[] 
        { 
            titleLabel, 
            ownerLabel, 
            _ownerTextBox, 
            repoLabel, 
            _repoNameTextBox, 
            _okButton, 
            _cancelButton 
        });
        
        this.AcceptButton = _okButton;
        this.CancelButton = _cancelButton;
    }
}
