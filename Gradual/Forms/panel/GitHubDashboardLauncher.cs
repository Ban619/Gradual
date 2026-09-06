using Gradual.Services.GitHub;
using Gradual.Repositories.GitHub;
using Gradual.Infrastructure;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Gradual.Forms;

/// <summary>
/// Helper class to launch the GitHub Dashboard from anywhere in the application
/// </summary>
public static class GitHubDashboardLauncher
{
    private static GDF? _currentDashboard;

    /// <summary>
    /// Opens the GitHub Dashboard as a separate window
    /// </summary>
    public static void OpenDashboard()
    {
        try
        {
            // Check if dashboard is already open
            if (_currentDashboard != null && !_currentDashboard.IsDisposed)
            {
                _currentDashboard.BringToFront();
                _currentDashboard.Focus();
                return;
            }

            // Get required services from ServiceContainer
            var githubService = ServiceContainer.GetService<IGitHubService>();
            var repositoryStore = ServiceContainer.GetService<IG_ReposStore>();
            var logger = ServiceContainer.GetService<ILogger>();
            var configuration = ServiceContainer.GetService<IConfiguration>();

            // Create and show dashboard
            _currentDashboard = new GDF(
                githubService,
                repositoryStore,
                logger,
                configuration
            );

            // Clean up reference when closed
            _currentDashboard.FormClosed += (s, e) => _currentDashboard = null;

            _currentDashboard.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to open GitHub Dashboard: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    /// <summary>
    /// Opens the GitHub Dashboard as a modal dialog
    /// </summary>
    public static DialogResult OpenDashboardModal()
    {
        try
        {
            // Get required services from ServiceContainer
            var githubService = ServiceContainer.GetService<IGitHubService>();
            var repositoryStore = ServiceContainer.GetService<IG_ReposStore>();
            var logger = ServiceContainer.GetService<ILogger>();
            var configuration = ServiceContainer.GetService<IConfiguration>();

            // Create and show dashboard as modal
            using var dashboard = new GDF(
                githubService,
                repositoryStore,
                logger,
                configuration
            );

            return dashboard.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to open GitHub Dashboard: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            return DialogResult.Cancel;
        }
    }

    /// <summary>
    /// Closes the dashboard if it's open
    /// </summary>
    public static void CloseDashboard()
    {
        if (_currentDashboard != null && !_currentDashboard.IsDisposed)
        {
            _currentDashboard.Close();
            _currentDashboard = null;
        }
    }

    /// <summary>
    /// Checks if the dashboard is currently open
    /// </summary>
    public static bool IsDashboardOpen => _currentDashboard != null && !_currentDashboard.IsDisposed;
}

/// <summary>
/// Extension methods for easy dashboard access from forms
/// </summary>
public static class FormGitHubExtensions
{
    /// <summary>
    /// Adds a GitHub Dashboard button to any form's menu strip or toolbar
    /// </summary>
    /// <param name="form">The parent form</param>
    /// <param name="menuStrip">Optional menu strip to add menu item to</param>
    /// <param name="toolStrip">Optional tool strip to add button to</param>
    public static void AddGitHubDashboardAccess(
        this Form form,
        MenuStrip? menuStrip = null,
        ToolStrip? toolStrip = null)
    {
        // Add to menu strip if provided
        if (menuStrip != null)
        {
            // Find or create Tools menu
            var toolsMenu = menuStrip.Items
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => item.Text == "Tools");

            if (toolsMenu == null)
            {
                toolsMenu = new ToolStripMenuItem("Tools");
                menuStrip.Items.Add(toolsMenu);
            }

            // Add GitHub Dashboard menu item
            var githubMenuItem = new ToolStripMenuItem
            {
                Text = "GitHub Dashboard",
                ShortcutKeys = Keys.Control | Keys.G,
                Image = null // Add icon if available
            };
            githubMenuItem.Click += (s, e) => GitHubDashboardLauncher.OpenDashboard();
            toolsMenu.DropDownItems.Add(githubMenuItem);
        }

        // Add to tool strip if provided
        if (toolStrip != null)
        {
            var githubButton = new ToolStripButton
            {
                Text = "🐙 GitHub",
                ToolTipText = "Open GitHub Dashboard",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            githubButton.Click += (s, e) => GitHubDashboardLauncher.OpenDashboard();
            toolStrip.Items.Add(githubButton);
        }
    }

    /// <summary>
    /// Creates a styled button to launch the GitHub Dashboard
    /// </summary>
    public static Button CreateGitHubDashboardButton()
    {
        var button = new Button
        {
            Text = "🐙 GitHub Dashboard",
            Size = new Size(150, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(88, 166, 255),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(68, 146, 235);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(48, 126, 215);
        button.Click += (s, e) => GitHubDashboardLauncher.OpenDashboard();

        return button;
    }
}
