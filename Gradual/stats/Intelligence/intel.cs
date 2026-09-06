using Gradual.Models;

namespace Gradual.BusinessLogic.Intelligence;

/// <summary>
/// Business intelligence and reporting logic for projects
/// </summary>
public class Intel
{
    /// <summary>
    /// Generates comprehensive project portfolio report
    /// </summary>
    public PortfolioReport GeneratePortfolioReport(List<ProjectRecord> projects)
    {
        var report = new PortfolioReport
        {
            GeneratedDate = DateTime.Now,
            TotalProjects = projects.Count,
            ProjectsByStatus = projects.GroupBy(p => p.Status)
                .ToDictionary(g => g.Key, g => g.Count()),
            ProjectsByPriority = projects.GroupBy(p => p.Priority)
                .ToDictionary(g => g.Key, g => g.Count()),
            ProjectsByClient = projects.GroupBy(p => p.Client)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        // Calculate metrics
        report.ActiveProjects = projects.Count(p => p.Status == "Active");
        report.CompletedProjects = projects.Count(p => p.Status == "Completed");
        report.OnHoldProjects = projects.Count(p => p.Status == "On Hold");
        report.HighPriorityProjects = projects.Count(p => p.Priority == "High");
        
        // Calculate completion rate
        if (report.TotalProjects > 0)
        {
            report.CompletionRate = (decimal)report.CompletedProjects / report.TotalProjects * 100;
        }

        // Identify trends
        report.Trends = AnalyzeTrends(projects);
        
        // Risk assessment
        report.RiskAssessment = AssessPortfolioRisk(projects);

        return report;
    }

    /// <summary>
    /// Generates detailed project analytics
    /// </summary>
    public ProjectAnalytics AnalyzeProject(ProjectRecord project, List<ProjectRecord> allProjects)
    {
        var analytics = new ProjectAnalytics
        {
            ProjectId = project.Id,
            ProjectName = project.ProjectName,
            AnalysisDate = DateTime.Now
        };

        // Age analysis
        analytics.ProjectAge = (DateTime.Now - project.CreatedAt).Days;
        analytics.DaysSinceLastUpdate = (DateTime.Now - project.UpdatedAt).Days;

        // Complexity score (0-100)
        analytics.ComplexityScore = CalculateComplexityScore(project);

        // Client relationship
        var clientProjects = allProjects.Where(p => p.Client == project.Client).ToList();
        analytics.ClientProjectCount = clientProjects.Count;
        analytics.ClientCompletionRate = clientProjects.Count > 0
            ? (decimal)clientProjects.Count(p => p.Status == "Completed") / clientProjects.Count * 100
            : 0;

        // Activity level
        analytics.ActivityLevel = DetermineActivityLevel(project);

        // Recommendations
        analytics.Recommendations = GenerateRecommendations(project, analytics);

        return analytics;
    }

    /// <summary>
    /// Calculates project complexity score
    /// </summary>
    private int CalculateComplexityScore(ProjectRecord project)
    {
        var score = 0;

        // Phase count (max 30 points)
        score += Math.Min(project.ProjectPhases.Count * 3, 30);

        // Attachment count (max 20 points)
        score += Math.Min(project.AttachmentPaths.Count * 2, 20);

        // Priority weight (max 25 points)
        score += project.Priority switch
        {
            "High" => 25,
            "Normal" => 15,
            "Low" => 5,
            _ => 0
        };

        // Status weight (max 25 points)
        score += project.Status switch
        {
            "Active" => 25,
            "On Hold" => 15,
            "Completed" => 5,
            _ => 0
        };

        return Math.Min(score, 100);
    }

    /// <summary>
    /// Determines project activity level
    /// </summary>
    private ActivityLevel DetermineActivityLevel(ProjectRecord project)
    {
        var daysSinceUpdate = (DateTime.Now - project.UpdatedAt).Days;

        if (daysSinceUpdate == 0) return ActivityLevel.VeryActive;
        if (daysSinceUpdate <= 3) return ActivityLevel.Active;
        if (daysSinceUpdate <= 7) return ActivityLevel.Moderate;
        if (daysSinceUpdate <= 30) return ActivityLevel.Low;
        return ActivityLevel.Dormant;
    }

    /// <summary>
    /// Generates actionable recommendations
    /// </summary>
    private List<string> GenerateRecommendations(ProjectRecord project, ProjectAnalytics analytics)
    {
        var recommendations = new List<string>();

        if (analytics.DaysSinceLastUpdate > 30)
        {
            recommendations.Add($"⚠️ No updates in {analytics.DaysSinceLastUpdate} days - review project status");
        }

        if (project.Status == "On Hold" && string.IsNullOrWhiteSpace(project.Notes))
        {
            recommendations.Add("📝 Add notes explaining why project is on hold");
        }

        if (project.Status == "Active" && string.IsNullOrWhiteSpace(project.FolderPath))
        {
            recommendations.Add("📁 Assign a working folder path for active project");
        }

        if (project.Priority == "High" && project.Status == "On Hold")
        {
            recommendations.Add("🚨 High priority project is on hold - immediate attention needed");
        }

        if (analytics.ComplexityScore > 75)
        {
            recommendations.Add("🔍 Complex project - consider breaking into sub-projects");
        }

        if (project.ProjectPhases.Count > 10)
        {
            recommendations.Add($"📊 {project.ProjectPhases.Count} phases detected - review for consolidation");
        }

        if (analytics.ClientCompletionRate < 50 && analytics.ClientProjectCount > 3)
        {
            recommendations.Add($"📉 Client '{project.Client}' has low completion rate ({analytics.ClientCompletionRate:F1}%) - review relationship");
        }

        return recommendations;
    }

    /// <summary>
    /// Analyzes portfolio trends
    /// </summary>
    private List<Trend> AnalyzeTrends(List<ProjectRecord> projects)
    {
        var trends = new List<Trend>();

        // Project creation trend (last 30 days)
        var recentProjects = projects.Count(p => p.CreatedAt >= DateTime.Now.AddDays(-30));
        if (recentProjects > 5)
        {
            trends.Add(new Trend
            {
                Category = "Growth",
                Description = $"{recentProjects} new projects in last 30 days",
                Indicator = TrendIndicator.Increasing,
                Impact = TrendImpact.Positive
            });
        }

        // On-hold trend
        var onHoldCount = projects.Count(p => p.Status == "On Hold");
        if (onHoldCount > projects.Count * 0.2)
        {
            trends.Add(new Trend
            {
                Category = "Risk",
                Description = $"{onHoldCount} projects on hold ({(decimal)onHoldCount / projects.Count * 100:F1}%)",
                Indicator = TrendIndicator.Stable,
                Impact = TrendImpact.Negative
            });
        }

        // Completion rate
        var completionRate = projects.Count > 0
            ? (decimal)projects.Count(p => p.Status == "Completed") / projects.Count * 100
            : 0;

        trends.Add(new Trend
        {
            Category = "Performance",
            Description = $"Overall completion rate: {completionRate:F1}%",
            Indicator = completionRate > 50 ? TrendIndicator.Increasing : TrendIndicator.Decreasing,
            Impact = completionRate > 50 ? TrendImpact.Positive : TrendImpact.Negative
        });

        return trends;
    }

    /// <summary>
    /// Assesses portfolio risk
    /// </summary>
    private RiskAssessment AssessPortfolioRisk(List<ProjectRecord> projects)
    {
        var assessment = new RiskAssessment();

        // Calculate risk factors
        var totalProjects = projects.Count;
        if (totalProjects == 0)
            return assessment;

        var onHoldPercentage = (decimal)projects.Count(p => p.Status == "On Hold") / totalProjects * 100;
        var highPriorityPercentage = (decimal)projects.Count(p => p.Priority == "High") / totalProjects * 100;
        var inactiveProjects = projects.Count(p => (DateTime.Now - p.UpdatedAt).Days > 30);

        // Determine risk level
        var riskScore = 0;
        
        if (onHoldPercentage > 20) riskScore += 30;
        if (highPriorityPercentage > 40) riskScore += 20;
        if (inactiveProjects > totalProjects * 0.15) riskScore += 25;
        if (projects.Any(p => p.Priority == "High" && p.Status == "On Hold")) riskScore += 25;

        assessment.RiskScore = riskScore;
        assessment.RiskLevel = riskScore switch
        {
            >= 75 => RiskLevel.Critical,
            >= 50 => RiskLevel.High,
            >= 25 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        // Add risk factors
        if (onHoldPercentage > 20)
        {
            assessment.RiskFactors.Add($"{onHoldPercentage:F1}% of projects are on hold");
        }

        if (inactiveProjects > 0)
        {
            assessment.RiskFactors.Add($"{inactiveProjects} projects with no recent activity");
        }

        if (projects.Any(p => p.Priority == "High" && p.Status == "On Hold"))
        {
            var count = projects.Count(p => p.Priority == "High" && p.Status == "On Hold");
            assessment.RiskFactors.Add($"{count} high-priority projects are on hold");
        }

        return assessment;
    }

    /// <summary>
    /// Generates KPI dashboard data
    /// </summary>
    public KPIDashboard GenerateKPIDashboard(List<ProjectRecord> projects)
    {
        var dashboard = new KPIDashboard
        {
            GeneratedDate = DateTime.Now
        };

        // Key metrics
        dashboard.KPIs.Add(new KPI
        {
            Name = "Total Active Projects",
            Value = projects.Count(p => p.Status == "Active"),
            Target = projects.Count * 0.7m,
            Unit = "projects",
            Status = KPIStatus.OnTrack
        });

        var completionRate = projects.Count > 0
            ? (decimal)projects.Count(p => p.Status == "Completed") / projects.Count * 100
            : 0;

        dashboard.KPIs.Add(new KPI
        {
            Name = "Completion Rate",
            Value = completionRate,
            Target = 60,
            Unit = "%",
            Status = completionRate >= 60 ? KPIStatus.OnTrack : KPIStatus.BelowTarget
        });

        dashboard.KPIs.Add(new KPI
        {
            Name = "High Priority Projects",
            Value = projects.Count(p => p.Priority == "High"),
            Target = projects.Count * 0.3m,
            Unit = "projects",
            Status = KPIStatus.OnTrack
        });

        var uniqueClients = projects.Select(p => p.Client).Distinct().Count();
        dashboard.KPIs.Add(new KPI
        {
            Name = "Active Clients",
            Value = uniqueClients,
            Target = 10,
            Unit = "clients",
            Status = uniqueClients >= 10 ? KPIStatus.OnTrack : KPIStatus.BelowTarget
        });

        return dashboard;
    }
}

/// <summary>
/// Portfolio report
/// </summary>
public class PortfolioReport
{
    public DateTime GeneratedDate { get; set; }
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int OnHoldProjects { get; set; }
    public int HighPriorityProjects { get; set; }
    public decimal CompletionRate { get; set; }
    public Dictionary<string, int> ProjectsByStatus { get; set; } = new();
    public Dictionary<string, int> ProjectsByPriority { get; set; } = new();
    public Dictionary<string, int> ProjectsByClient { get; set; } = new();
    public List<Trend> Trends { get; set; } = new();
    public RiskAssessment RiskAssessment { get; set; } = new();

    // Derived metrics used by DashboardForm
    public int TotalClients => ProjectsByClient.Count;
    public decimal AverageProjectsPerClient => TotalClients > 0
        ? (decimal)TotalProjects / TotalClients : 0m;
    public decimal ActiveProjectsRate => TotalProjects > 0
        ? (decimal)ActiveProjects / TotalProjects * 100 : 0m;
}

/// <summary>
/// Project analytics
/// </summary>
public class ProjectAnalytics
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
    public int ProjectAge { get; set; }
    public int DaysSinceLastUpdate { get; set; }
    public int ComplexityScore { get; set; }
    public int ClientProjectCount { get; set; }
    public decimal ClientCompletionRate { get; set; }
    public ActivityLevel ActivityLevel { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

public enum ActivityLevel
{
    Dormant,
    Low,
    Moderate,
    Active,
    VeryActive
}

public class Trend
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TrendIndicator Indicator { get; set; }
    public TrendImpact Impact { get; set; }
}

public enum TrendIndicator
{
    Increasing,
    Stable,
    Decreasing
}

public enum TrendImpact
{
    Positive,
    Neutral,
    Negative
}

public class RiskAssessment
{
    public int RiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public List<string> RiskFactors { get; set; } = new();
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public class KPIDashboard
{
    public DateTime GeneratedDate { get; set; }
    public List<KPI> KPIs { get; set; } = new();
}

public class KPI
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal Target { get; set; }
    public string Unit { get; set; } = string.Empty;
    public KPIStatus Status { get; set; }
    public decimal Achievement => Target > 0 ? (Value / Target) * 100 : 0;
}

public enum KPIStatus
{
    BelowTarget,
    OnTrack,
    ExceedingTarget
}
