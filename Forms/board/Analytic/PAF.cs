using Gradual.Infrastructure;
using Gradual.BusinessLogic.Intelligence;
using Gradual.Interfaces;
using Gradual.Models;

namespace Gradual.Forms;

/// <summary>
/// Detailed project analytics form
/// Shows individual project analysis, complexity scores, and recommendations
/// </summary>
public partial class PAF : Form
{
    private readonly IProjectService _projectService;
    private readonly Intel _biEngine;
    private List<ProjectRecord> _projects = new();

    public PAF()
    {
        InitializeComponent();
        
        _projectService = ServiceContainer.GetRequiredService<IProjectService>();
        _biEngine = ServiceContainer.GetRequiredService<Intel>();
        
        SetupForm();
        _ = LoadProjectsAsync();
    }

    private void SetupForm()
    {
        this.Text = "Project Analytics";
        this.Size = new Size(1200, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(236, 240, 241);
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            
            var result = await _projectService.GetAllProjectsAsync();
            if (result.Success && result.Data != null)
            {
                _projects = result.Data;
                BuildAnalyticsView();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading projects: {ex.Message}", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void BuildAnalyticsView()
    {
        this.Controls.Clear();

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(236, 240, 241)
        };

        // Header
        var header = new Label
        {
            Text = "Project Analytics & Insights",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(20, 20),
            AutoSize = true
        };
        mainPanel.Controls.Add(header);

        // Project list with analytics
        var listPanel = CreateProjectAnalyticsList();
        listPanel.Location = new Point(20, 70);
        mainPanel.Controls.Add(listPanel);

        this.Controls.Add(mainPanel);
    }

    private Panel CreateProjectAnalyticsList()
    {
        var panel = new Panel
        {
            Size = new Size(1140, 650),
            BackColor = Color.White
        };

        var titleLabel = new Label
        {
            Text = "Project Complexity & Performance Analysis",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(15, 15),
            AutoSize = true
        };
        panel.Controls.Add(titleLabel);

        // Create scrollable container for project cards
        var scrollPanel = new Panel
        {
            Location = new Point(15, 50),
            Size = new Size(1110, 580),
            AutoScroll = true,
            BackColor = Color.White
        };

        int yPos = 10;
        foreach (var project in _projects.Take(20))
        {
            var analytics = _biEngine.AnalyzeProject(project, _projects);
            var card = CreateProjectAnalyticsCard(project, analytics);
            card.Location = new Point(10, yPos);
            scrollPanel.Controls.Add(card);
            yPos += card.Height + 15;
        }

        panel.Controls.Add(scrollPanel);

        return panel;
    }

    private Panel CreateProjectAnalyticsCard(ProjectRecord project, ProjectAnalytics analytics)
    {
        var card = new Panel
        {
            Size = new Size(1060, 180),
            BackColor = Color.FromArgb(249, 249, 249),
            BorderStyle = BorderStyle.FixedSingle
        };

        // Project name and status
        var nameLabel = new Label
        {
            Text = project.ProjectName,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(15, 10),
            Size = new Size(600, 25)
        };
        card.Controls.Add(nameLabel);

        var statusLabel = new Label
        {
            Text = $"Status: {project.Status}",
            Font = new Font("Segoe UI", 9),
            ForeColor = GetStatusColor(project.Status),
            Location = new Point(620, 12),
            AutoSize = true
        };
        card.Controls.Add(statusLabel);

        var priorityLabel = new Label
        {
            Text = $"Priority: {project.Priority}",
            Font = new Font("Segoe UI", 9),
            ForeColor = GetPriorityColor(project.Priority),
            Location = new Point(750, 12),
            AutoSize = true
        };
        card.Controls.Add(priorityLabel);

        // Client
        if (!string.IsNullOrEmpty(project.Client))
        {
            var clientLabel = new Label
            {
                Text = $"Client: {project.Client}",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(127, 140, 141),
                Location = new Point(15, 40),
                AutoSize = true
            };
            card.Controls.Add(clientLabel);
        }

        // Complexity Score
        var complexityLabel = new Label
        {
            Text = "Complexity Score",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(127, 140, 141),
            Location = new Point(15, 70),
            AutoSize = true
        };
        card.Controls.Add(complexityLabel);

        var complexityValue = new Label
        {
            Text = $"{analytics.ComplexityScore}/100",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = GetComplexityColor(analytics.ComplexityScore),
            Location = new Point(15, 90),
            AutoSize = true
        };
        card.Controls.Add(complexityValue);

        // Complexity bar
        var complexityBar = new ProgressBar
        {
            Location = new Point(120, 100),
            Size = new Size(200, 20),
            Minimum = 0,
            Maximum = 100,
            Value = Math.Min(analytics.ComplexityScore, 100),
            Style = ProgressBarStyle.Continuous
        };
        card.Controls.Add(complexityBar);

        // Activity Level
        var activityLabel = new Label
        {
            Text = $"Activity: {analytics.ActivityLevel}",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(350, 85),
            AutoSize = true
        };
        card.Controls.Add(activityLabel);

        // Project Age
        var ageLabel = new Label
        {
            Text = $"Age: {analytics.ProjectAge} days",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(127, 140, 141),
            Location = new Point(350, 105),
            AutoSize = true
        };
        card.Controls.Add(ageLabel);

        // Client Stats
        if (analytics.ClientProjectCount > 0)
        {
            var clientStatsLabel = new Label
            {
                Text = $"Client Portfolio: {analytics.ClientProjectCount} projects ({analytics.ClientCompletionRate:F0}% completed)",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(127, 140, 141),
                Location = new Point(550, 85),
                AutoSize = true
            };
            card.Controls.Add(clientStatsLabel);
        }

        // Recommendations
        if (analytics.Recommendations.Any())
        {
            var recLabel = new Label
            {
                Text = "💡 " + string.Join(" • ", analytics.Recommendations.Take(2)),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(52, 152, 219),
                Location = new Point(15, 135),
                Size = new Size(1030, 35),
                AutoEllipsis = true
            };
            card.Controls.Add(recLabel);
        }

        // View details button
        var detailsButton = new Button
        {
            Text = "View Details",
            Size = new Size(120, 30),
            Location = new Point(920, 140),
            BackColor = Color.FromArgb(52, 152, 219),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        detailsButton.FlatAppearance.BorderSize = 0;
        detailsButton.Click += (s, e) => ShowProjectDetails(project, analytics);
        card.Controls.Add(detailsButton);

        return card;
    }

    private void ShowProjectDetails(ProjectRecord project, ProjectAnalytics analytics)
    {
        var detailsMessage = $"Project: {project.ProjectName}\n\n";
        detailsMessage += $"Complexity Score: {analytics.ComplexityScore}/100\n";
        detailsMessage += $"Activity Level: {analytics.ActivityLevel}\n";
        detailsMessage += $"Project Age: {analytics.ProjectAge} days\n\n";
        
        if (analytics.ClientProjectCount > 0)
        {
            detailsMessage += $"Client: {project.Client}\n";
            detailsMessage += $"Total Client Projects: {analytics.ClientProjectCount}\n";
            detailsMessage += $"Client Completion Rate: {analytics.ClientCompletionRate:F1}%\n\n";
        }

        detailsMessage += "Recommendations:\n";
        foreach (var rec in analytics.Recommendations)
        {
            detailsMessage += $"• {rec}\n";
        }

        MessageBox.Show(detailsMessage, "Project Analytics Details", 
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private Color GetStatusColor(string status)
    {
        return status switch
        {
            "Active" => Color.FromArgb(46, 204, 113),
            "Completed" => Color.FromArgb(52, 152, 219),
            "On Hold" => Color.FromArgb(241, 196, 15),
            _ => Color.FromArgb(127, 140, 141)
        };
    }

    private Color GetPriorityColor(string priority)
    {
        return priority switch
        {
            "High" => Color.FromArgb(231, 76, 60),
            "Normal" => Color.FromArgb(52, 152, 219),
            "Low" => Color.FromArgb(149, 165, 166),
            _ => Color.FromArgb(127, 140, 141)
        };
    }

    private Color GetComplexityColor(int complexity)
    {
        if (complexity >= 75) return Color.FromArgb(231, 76, 60); // Red - High
        if (complexity >= 50) return Color.FromArgb(241, 196, 15); // Yellow - Medium
        if (complexity >= 25) return Color.FromArgb(52, 152, 219); // Blue - Low-Medium
        return Color.FromArgb(46, 204, 113); // Green - Low
    }
}
