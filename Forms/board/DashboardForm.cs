using System.Drawing.Drawing2D;
using Gradual.Infrastructure;
using Gradual.BusinessLogic.Intelligence;
using Gradual.Interfaces;
using Gradual.Models;

namespace Gradual.Forms;

/// <summary>
/// Business Intelligence Dashboard Form
/// Displays analytics, KPIs, trends, and portfolio metrics
/// </summary>
public partial class DashboardForm : Form
{
    private readonly IProjectService _projectService;
    private readonly Intel _biEngine;
    private List<ProjectRecord> _projects = new();
    
    // Dashboard metrics
    private PortfolioReport? _portfolioReport;
    private KPIDashboard? _kpiDashboard;
    
    // Colors for charts
    private readonly Color _primaryColor = Color.FromArgb(52, 152, 219);
    private readonly Color _successColor = Color.FromArgb(46, 204, 113);
    private readonly Color _warningColor = Color.FromArgb(241, 196, 15);
    private readonly Color _dangerColor = Color.FromArgb(231, 76, 60);
    private readonly Color _infoColor = Color.FromArgb(155, 89, 182);

    public DashboardForm()
    {
        InitializeComponent();
        
        // Get services from DI container
        _projectService = ServiceContainer.GetRequiredService<IProjectService>();
        _biEngine = ServiceContainer.GetRequiredService<Intel>();
        
        // Setup form
        SetupForm();
        
        // Load data
        _ = LoadDashboardDataAsync();
    }

    private void SetupForm()
    {
        this.Text = "Business Intelligence Dashboard";
        this.Size = new Size(1400, 900);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(236, 240, 241);
        this.Font = new Font("Segoe UI", 9F);
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            // Show loading indicator
            Cursor = Cursors.WaitCursor;
            
            // Load projects
            var result = await _projectService.GetAllProjectsAsync();
            if (result.Success && result.Data != null)
            {
                _projects = result.Data;
                
                // Generate reports
                _portfolioReport = _biEngine.GeneratePortfolioReport(_projects);
                _kpiDashboard = _biEngine.GenerateKPIDashboard(_projects);
                
                // Refresh all visualizations
                RefreshDashboard();
            }
            else
            {
                MessageBox.Show($"Failed to load projects: {result.Message}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading dashboard: {ex.Message}", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RefreshDashboard()
    {
        if (_portfolioReport == null || _kpiDashboard == null)
            return;

        // Clear existing controls
        this.Controls.Clear();

        // Create scrollable panel
        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(236, 240, 241)
        };

        int yPos = 20;
        int leftMargin = 20;

        // Add header
        var header = CreateHeader();
        header.Location = new Point(leftMargin, yPos);
        mainPanel.Controls.Add(header);
        yPos += header.Height + 20;

        // Add KPI Cards Row
        var kpiPanel = CreateKPICards();
        kpiPanel.Location = new Point(leftMargin, yPos);
        mainPanel.Controls.Add(kpiPanel);
        yPos += kpiPanel.Height + 20;

        // Add Charts Row
        var chartsPanel = CreateChartsRow();
        chartsPanel.Location = new Point(leftMargin, yPos);
        mainPanel.Controls.Add(chartsPanel);
        yPos += chartsPanel.Height + 20;

        // Add Portfolio Metrics
        var portfolioPanel = CreatePortfolioMetrics();
        portfolioPanel.Location = new Point(leftMargin, yPos);
        mainPanel.Controls.Add(portfolioPanel);
        yPos += portfolioPanel.Height + 20;

        // Add Risk Assessment
        var riskPanel = CreateRiskAssessment();
        riskPanel.Location = new Point(leftMargin, yPos);
        mainPanel.Controls.Add(riskPanel);
        yPos += riskPanel.Height + 20;

        // Add Trends
        var trendsPanel = CreateTrendsPanel();
        trendsPanel.Location = new Point(leftMargin, yPos);
        mainPanel.Controls.Add(trendsPanel);
        yPos += trendsPanel.Height + 30;

        this.Controls.Add(mainPanel);
    }

    private Panel CreateHeader()
    {
        var panel = new Panel
        {
            Size = new Size(1340, 80),
            BackColor = Color.White
        };

        var titleLabel = new Label
        {
            Text = "Business Intelligence Dashboard",
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(20, 15),
            AutoSize = true
        };
        panel.Controls.Add(titleLabel);

        var subtitleLabel = new Label
        {
            Text = $"Portfolio Overview · {DateTime.Now:MMMM dd, yyyy}",
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.FromArgb(127, 140, 141),
            Location = new Point(20, 50),
            AutoSize = true
        };
        panel.Controls.Add(subtitleLabel);

        var refreshButton = new Button
        {
            Text = "↻ Refresh",
            Size = new Size(120, 40),
            Location = new Point(1200, 20),
            BackColor = _primaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        refreshButton.FlatAppearance.BorderSize = 0;
        refreshButton.Click += async (s, e) => await LoadDashboardDataAsync();
        panel.Controls.Add(refreshButton);

        return panel;
    }

    private Panel CreateKPICards()
    {
        var panel = new Panel
        {
            Size = new Size(1340, 140),
            BackColor = Color.Transparent
        };

        if (_kpiDashboard == null) return panel;

        int cardWidth = 320;
        int cardSpacing = 10;
        int xPos = 0;

        foreach (var kpi in _kpiDashboard.KPIs.Take(4))
        {
            var card = CreateKPICard(kpi, cardWidth);
            card.Location = new Point(xPos, 0);
            panel.Controls.Add(card);
            xPos += cardWidth + cardSpacing;
        }

        return panel;
    }

    private Panel CreateKPICard(KPI kpi, int width)
    {
        var card = new Panel
        {
            Size = new Size(width, 130),
            BackColor = Color.White
        };

        // Add shadow effect
        card.Paint += (s, e) =>
        {
            var rect = card.ClientRectangle;
            using (var path = CreateRoundedRectangle(rect, 8))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(Color.White))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }
        };

        // KPI Name
        var nameLabel = new Label
        {
            Text = kpi.Name,
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(127, 140, 141),
            Location = new Point(15, 15),
            AutoSize = true
        };
        card.Controls.Add(nameLabel);

        // KPI Value
        var valueLabel = new Label
        {
            Text = $"{kpi.Value} {kpi.Unit}",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(15, 40),
            AutoSize = true
        };
        card.Controls.Add(valueLabel);

        // Target and Achievement
        var targetLabel = new Label
        {
            Text = $"Target: {kpi.Target} {kpi.Unit}",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(149, 165, 166),
            Location = new Point(15, 85),
            AutoSize = true
        };
        card.Controls.Add(targetLabel);

        // Achievement indicator
        var achievementColor = kpi.Achievement >= 100 ? _successColor :
                              kpi.Achievement >= 75 ? _warningColor : _dangerColor;
        
        var achievementLabel = new Label
        {
            Text = $"⬤ {kpi.Achievement:F0}%",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = achievementColor,
            Location = new Point(15, 105),
            AutoSize = true
        };
        card.Controls.Add(achievementLabel);

        // Status indicator
        var statusText = kpi.Status == KPIStatus.ExceedingTarget ? "↑ Exceeding" :
                        kpi.Status == KPIStatus.OnTrack ? "→ On Track" : "↓ Below Target";
        var statusLabel = new Label
        {
            Text = statusText,
            Font = new Font("Segoe UI", 9),
            ForeColor = achievementColor,
            Location = new Point(width - 120, 105),
            AutoSize = true
        };
        card.Controls.Add(statusLabel);

        return card;
    }

    private Panel CreateChartsRow()
    {
        var panel = new Panel
        {
            Size = new Size(1340, 350),
            BackColor = Color.Transparent
        };

        // Status Distribution Chart
        var statusChart = CreateStatusChart();
        statusChart.Location = new Point(0, 0);
        panel.Controls.Add(statusChart);

        // Priority Distribution Chart
        var priorityChart = CreatePriorityChart();
        priorityChart.Location = new Point(450, 0);
        panel.Controls.Add(priorityChart);

        // Completion Trend
        var completionChart = CreateCompletionChart();
        completionChart.Location = new Point(900, 0);
        panel.Controls.Add(completionChart);

        return panel;
    }

    private Panel CreateStatusChart()
    {
        var panel = new Panel
        {
            Size = new Size(430, 340),
            BackColor = Color.White
        };

        var titleLabel = new Label
        {
            Text = "Projects by Status",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(15, 15),
            AutoSize = true
        };
        panel.Controls.Add(titleLabel);

        if (_portfolioReport != null)
        {
            var chartPanel = new Panel
            {
                Location = new Point(15, 50),
                Size = new Size(400, 270),
                BackColor = Color.White
            };

            chartPanel.Paint += (s, e) =>
            {
                DrawPieChart(e.Graphics, _portfolioReport.ProjectsByStatus, 
                    new Rectangle(20, 20, 180, 180), new Point(220, 40));
            };

            panel.Controls.Add(chartPanel);
        }

        return panel;
    }

    private Panel CreatePriorityChart()
    {
        var panel = new Panel
        {
            Size = new Size(430, 340),
            BackColor = Color.White
        };

        var titleLabel = new Label
        {
            Text = "Projects by Priority",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(15, 15),
            AutoSize = true
        };
        panel.Controls.Add(titleLabel);

        if (_portfolioReport != null)
        {
            var chartPanel = new Panel
            {
                Location = new Point(15, 50),
                Size = new Size(400, 270),
                BackColor = Color.White
            };

            chartPanel.Paint += (s, e) =>
            {
                DrawBarChart(e.Graphics, _portfolioReport.ProjectsByPriority,
                    new Rectangle(20, 20, 360, 220));
            };

            panel.Controls.Add(chartPanel);
        }

        return panel;
    }

    private Panel CreateCompletionChart()
    {
        var panel = new Panel
        {
            Size = new Size(430, 340),
            BackColor = Color.White
        };

        var titleLabel = new Label
        {
            Text = "Completion Rate",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(15, 15),
            AutoSize = true
        };
        panel.Controls.Add(titleLabel);

        if (_portfolioReport != null)
        {
            var completionRate = _portfolioReport.CompletionRate;
            var activeRate = _portfolioReport.ActiveProjectsRate;
            
            var chartPanel = new Panel
            {
                Location = new Point(15, 50),
                Size = new Size(400, 270),
                BackColor = Color.White
            };

            chartPanel.Paint += (s, e) =>
            {
                DrawCompletionGauge(e.Graphics, completionRate, 
                    new Rectangle(80, 20, 240, 240));
            };

            panel.Controls.Add(chartPanel);
        }

        return panel;
    }

    private Panel CreatePortfolioMetrics()
    {
        var panel = new Panel
        {
            Size = new Size(1340, 200),
            BackColor = Color.White
        };

        var titleLabel = new Label
        {
            Text = "Portfolio Metrics",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(20, 15),
            AutoSize = true
        };
        panel.Controls.Add(titleLabel);

        if (_portfolioReport != null)
        {
            int yPos = 55;
            int colWidth = 330;

            // Column 1
            AddMetricLabel(panel, "Total Projects:", _portfolioReport.TotalProjects.ToString(), 
                20, yPos);
            AddMetricLabel(panel, "Active Projects:", _portfolioReport.ActiveProjects.ToString(), 
                20, yPos + 30);
            AddMetricLabel(panel, "Completion Rate:", $"{_portfolioReport.CompletionRate:F1}%", 
                20, yPos + 60);

            // Column 2
            AddMetricLabel(panel, "Total Clients:", _portfolioReport.TotalClients.ToString(), 
                20 + colWidth, yPos);
            AddMetricLabel(panel, "Avg Projects/Client:", 
                $"{_portfolioReport.AverageProjectsPerClient:F1}", 
                20 + colWidth, yPos + 30);
            AddMetricLabel(panel, "Active Rate:", $"{_portfolioReport.ActiveProjectsRate:F1}%", 
                20 + colWidth, yPos + 60);

            // Column 3
            var riskColor = _portfolioReport.RiskAssessment.RiskLevel == RiskLevel.Critical ? _dangerColor :
                           _portfolioReport.RiskAssessment.RiskLevel == RiskLevel.High ? _warningColor :
                           _portfolioReport.RiskAssessment.RiskLevel == RiskLevel.Medium ? _infoColor :
                           _successColor;

            AddMetricLabel(panel, "Risk Level:", 
                _portfolioReport.RiskAssessment.RiskLevel.ToString(), 
                20 + colWidth * 2, yPos, riskColor);
            AddMetricLabel(panel, "Risk Score:", 
                $"{_portfolioReport.RiskAssessment.RiskScore:F0}/100", 
                20 + colWidth * 2, yPos + 30);

            // Column 4
            if (_kpiDashboard != null && _kpiDashboard.KPIs.Any())
            {
                var firstKPI = _kpiDashboard.KPIs.First();
                AddMetricLabel(panel, "KPI Status:", firstKPI.Status.ToString(), 
                    20 + colWidth * 3, yPos);
            }
        }

        return panel;
    }

    private Panel CreateRiskAssessment()
    {
        var panel = new Panel
        {
            Size = new Size(1340, 180),
            BackColor = Color.White
        };

        var titleLabel = new Label
        {
            Text = "Risk Assessment",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(20, 15),
            AutoSize = true
        };
        panel.Controls.Add(titleLabel);

        if (_portfolioReport?.RiskAssessment != null)
        {
            var risk = _portfolioReport.RiskAssessment;
            int yPos = 55;

            // Risk factors
            foreach (var factor in risk.RiskFactors.Take(3))
            {
                var factorLabel = new Label
                {
                    Text = $"⚠ {factor}",
                    Font = new Font("Segoe UI", 10),
                    ForeColor = _dangerColor,
                    Location = new Point(20, yPos),
                    AutoSize = true,
                    MaximumSize = new Size(1300, 0)
                };
                panel.Controls.Add(factorLabel);
                yPos += 30;
            }
        }

        return panel;
    }

    private Panel CreateTrendsPanel()
    {
        var panel = new Panel
        {
            Size = new Size(1340, 200),
            BackColor = Color.White
        };

        var titleLabel = new Label
        {
            Text = "Trends & Insights",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Location = new Point(20, 15),
            AutoSize = true
        };
        panel.Controls.Add(titleLabel);

        if (_portfolioReport?.Trends != null)
        {
            int yPos = 55;

            foreach (var trend in _portfolioReport.Trends.Take(4))
            {
                var icon = trend.Impact == TrendImpact.Positive ? "↗" :
                          trend.Impact == TrendImpact.Negative ? "↘" : "→";
                var color = trend.Impact == TrendImpact.Positive ? _successColor :
                           trend.Impact == TrendImpact.Negative ? _dangerColor : _infoColor;

                var trendLabel = new Label
                {
                    Text = $"{icon} [{trend.Category}] {trend.Description}",
                    Font = new Font("Segoe UI", 10),
                    ForeColor = color,
                    Location = new Point(20, yPos),
                    AutoSize = true,
                    MaximumSize = new Size(1300, 0)
                };
                panel.Controls.Add(trendLabel);
                yPos += 35;
            }
        }

        return panel;
    }

    // Helper methods for drawing charts
    private void DrawPieChart(Graphics g, Dictionary<string, int> data, Rectangle bounds, Point legendStart)
    {
        if (data == null || !data.Any()) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        int total = data.Values.Sum();
        float startAngle = 0;

        var colors = new[] { _primaryColor, _successColor, _warningColor, _dangerColor, _infoColor };
        int colorIndex = 0;

        int legendY = legendStart.Y;

        foreach (var item in data)
        {
            float sweepAngle = (item.Value / (float)total) * 360;
            var color = colors[colorIndex % colors.Length];

            using (var brush = new SolidBrush(color))
            {
                g.FillPie(brush, bounds, startAngle, sweepAngle);
            }

            // Draw legend
            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, legendStart.X, legendY, 15, 15);
            }

            var percentage = (item.Value / (float)total) * 100;
            var legendText = $"{item.Key}: {item.Value} ({percentage:F0}%)";
            g.DrawString(legendText, new Font("Segoe UI", 9), Brushes.Black, 
                legendStart.X + 20, legendY);

            legendY += 25;
            startAngle += sweepAngle;
            colorIndex++;
        }
    }

    private void DrawBarChart(Graphics g, Dictionary<string, int> data, Rectangle bounds)
    {
        if (data == null || !data.Any()) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        int maxValue = data.Values.Max();
        int barCount = data.Count;
        int barWidth = (bounds.Width - 40) / barCount;
        int x = bounds.X + 20;

        var colors = new[] { _successColor, _warningColor, _dangerColor };
        int colorIndex = 0;

        foreach (var item in data)
        {
            int barHeight = (int)((item.Value / (float)maxValue) * (bounds.Height - 40));
            int y = bounds.Bottom - barHeight - 20;

            var color = colors[colorIndex % colors.Length];
            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, x, y, barWidth - 10, barHeight);
            }

            // Draw value
            g.DrawString(item.Value.ToString(), new Font("Segoe UI", 9, FontStyle.Bold), 
                Brushes.Black, x + (barWidth - 10) / 2 - 10, y - 20);

            // Draw label
            g.DrawString(item.Key, new Font("Segoe UI", 8), Brushes.Black, 
                x, bounds.Bottom - 15);

            x += barWidth;
            colorIndex++;
        }
    }

    private void DrawCompletionGauge(Graphics g, decimal percentage, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw background arc
        using (var pen = new Pen(Color.FromArgb(236, 240, 241), 20))
        {
            g.DrawArc(pen, bounds, 180, 180);
        }

        // Draw completion arc
        var color = percentage >= 75 ? _successColor :
                   percentage >= 50 ? _warningColor : _dangerColor;

        float sweepAngle = (float)percentage / 100 * 180;
        using (var pen = new Pen(color, 20))
        {
            g.DrawArc(pen, bounds, 180, sweepAngle);
        }

        // Draw percentage text
        var text = $"{percentage:F0}%";
        var font = new Font("Segoe UI", 32, FontStyle.Bold);
        var textSize = g.MeasureString(text, font);
        var textX = bounds.X + (bounds.Width - textSize.Width) / 2;
        var textY = bounds.Y + bounds.Height / 2 + 20;
        
        g.DrawString(text, font, new SolidBrush(color), textX, textY);

        // Draw label
        var label = "Completed";
        var labelFont = new Font("Segoe UI", 11);
        var labelSize = g.MeasureString(label, labelFont);
        var labelX = bounds.X + (bounds.Width - labelSize.Width) / 2;
        var labelY = textY + 45;
        
        g.DrawString(label, labelFont, Brushes.Gray, labelX, labelY);
    }

    private void AddMetricLabel(Panel panel, string label, string value, int x, int y, Color? valueColor = null)
    {
        var labelControl = new Label
        {
            Text = label,
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(127, 140, 141),
            Location = new Point(x, y),
            AutoSize = true
        };
        panel.Controls.Add(labelControl);

        var valueControl = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = valueColor ?? Color.FromArgb(44, 62, 80),
            Location = new Point(x + 180, y),
            AutoSize = true
        };
        panel.Controls.Add(valueControl);
    }

    private GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        Size size = new Size(diameter, diameter);
        Rectangle arc = new Rectangle(bounds.Location, size);
        GraphicsPath path = new GraphicsPath();

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}
