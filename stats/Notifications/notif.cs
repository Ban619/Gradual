namespace Gradual.BusinessLogic.Notifications;

/// <summary>
/// Manages business logic for notifications and alerts
/// </summary>
public class Notif
{
    private readonly List<NotificationRule> _rules = new();
    private readonly List<Notification> _notifications = new();
    private readonly List<Alert> _activeAlerts = new();

    public Notif()
    {
        InitializeDefaultRules();
    }

    /// <summary>
    /// Initializes default notification rules
    /// </summary>
    private void InitializeDefaultRules()
    {
        // Budget alert rules
        _rules.Add(new NotificationRule
        {
            RuleId = Guid.NewGuid(),
            Name = "Budget Overrun Warning",
            Category = NotificationCategory.Budget,
            Severity = NotificationSeverity.High,
            Condition = "BudgetUtilization > 90",
            MessageTemplate = "Project '{ProjectName}' has exceeded 90% of budget"
        });

        _rules.Add(new NotificationRule
        {
            RuleId = Guid.NewGuid(),
            Name = "Budget Threshold Exceeded",
            Category = NotificationCategory.Budget,
            Severity = NotificationSeverity.Critical,
            Condition = "BudgetUtilization > 100",
            MessageTemplate = "Project '{ProjectName}' has exceeded budget limit"
        });

        // Deadline alert rules
        _rules.Add(new NotificationRule
        {
            RuleId = Guid.NewGuid(),
            Name = "Deadline Approaching",
            Category = NotificationCategory.Deadline,
            Severity = NotificationSeverity.Medium,
            Condition = "DaysUntilDeadline <= 7",
            MessageTemplate = "Project '{ProjectName}' deadline is in {DaysUntilDeadline} days"
        });

        _rules.Add(new NotificationRule
        {
            RuleId = Guid.NewGuid(),
            Name = "Deadline Imminent",
            Category = NotificationCategory.Deadline,
            Severity = NotificationSeverity.High,
            Condition = "DaysUntilDeadline <= 2",
            MessageTemplate = "URGENT: Project '{ProjectName}' deadline is in {DaysUntilDeadline} days"
        });

        _rules.Add(new NotificationRule
        {
            RuleId = Guid.NewGuid(),
            Name = "Deadline Overdue",
            Category = NotificationCategory.Deadline,
            Severity = NotificationSeverity.Critical,
            Condition = "DaysUntilDeadline < 0",
            MessageTemplate = "OVERDUE: Project '{ProjectName}' is {DaysOverdue} days past deadline"
        });

        // Status change rules
        _rules.Add(new NotificationRule
        {
            RuleId = Guid.NewGuid(),
            Name = "Project On Hold",
            Category = NotificationCategory.StatusChange,
            Severity = NotificationSeverity.Medium,
            Condition = "StatusChanged == 'On Hold'",
            MessageTemplate = "Project '{ProjectName}' has been placed on hold"
        });

        _rules.Add(new NotificationRule
        {
            RuleId = Guid.NewGuid(),
            Name = "Project Completed",
            Category = NotificationCategory.StatusChange,
            Severity = NotificationSeverity.Info,
            Condition = "StatusChanged == 'Completed'",
            MessageTemplate = "Project '{ProjectName}' has been completed"
        });

        // Inactivity rules
        _rules.Add(new NotificationRule
        {
            RuleId = Guid.NewGuid(),
            Name = "Inactive Project Warning",
            Category = NotificationCategory.Activity,
            Severity = NotificationSeverity.Low,
            Condition = "DaysSinceLastUpdate > 30",
            MessageTemplate = "Project '{ProjectName}' has had no activity for {DaysSinceLastUpdate} days"
        });
    }

    /// <summary>
    /// Evaluates project conditions and generates alerts
    /// </summary>
    public AlertEvaluationResult EvaluateProject(
        Guid projectId,
        string projectName,
        Dictionary<string, object> conditions)
    {
        var result = new AlertEvaluationResult
        {
            ProjectId = projectId,
            EvaluationDate = DateTime.Now
        };

        foreach (var rule in _rules.Where(r => r.IsEnabled))
        {
            if (EvaluateCondition(rule.Condition, conditions))
            {
                var alert = CreateAlert(projectId, projectName, rule, conditions);
                
                // Check if alert already exists
                if (!_activeAlerts.Any(a => a.ProjectId == projectId && 
                                            a.RuleId == rule.RuleId && 
                                            a.Status == AlertStatus.Active))
                {
                    _activeAlerts.Add(alert);
                    result.NewAlerts.Add(alert);
                }
            }
        }

        result.TotalActiveAlerts = _activeAlerts.Count(a => a.ProjectId == projectId && a.Status == AlertStatus.Active);
        return result;
    }

    /// <summary>
    /// Evaluates a condition against provided data
    /// </summary>
    private bool EvaluateCondition(string condition, Dictionary<string, object> data)
    {
        try
        {
            // Simple condition evaluation (in production, use expression evaluator)
            if (condition.Contains("BudgetUtilization > 100"))
            {
                if (data.ContainsKey("BudgetUtilization"))
                {
                    var value = Convert.ToDecimal(data["BudgetUtilization"]);
                    return value > 100;
                }
            }
            else if (condition.Contains("BudgetUtilization > 90"))
            {
                if (data.ContainsKey("BudgetUtilization"))
                {
                    var value = Convert.ToDecimal(data["BudgetUtilization"]);
                    return value > 90;
                }
            }
            else if (condition.Contains("DaysUntilDeadline <= 2"))
            {
                if (data.ContainsKey("DaysUntilDeadline"))
                {
                    var value = Convert.ToInt32(data["DaysUntilDeadline"]);
                    return value <= 2;
                }
            }
            else if (condition.Contains("DaysUntilDeadline <= 7"))
            {
                if (data.ContainsKey("DaysUntilDeadline"))
                {
                    var value = Convert.ToInt32(data["DaysUntilDeadline"]);
                    return value <= 7;
                }
            }
            else if (condition.Contains("DaysUntilDeadline < 0"))
            {
                if (data.ContainsKey("DaysUntilDeadline"))
                {
                    var value = Convert.ToInt32(data["DaysUntilDeadline"]);
                    return value < 0;
                }
            }
            else if (condition.Contains("DaysSinceLastUpdate > 30"))
            {
                if (data.ContainsKey("DaysSinceLastUpdate"))
                {
                    var value = Convert.ToInt32(data["DaysSinceLastUpdate"]);
                    return value > 30;
                }
            }
            else if (condition.Contains("StatusChanged"))
            {
                if (data.ContainsKey("StatusChanged"))
                {
                    var expectedStatus = condition.Split("'")[1];
                    return data["StatusChanged"].ToString() == expectedStatus;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates an alert from a rule
    /// </summary>
    private Alert CreateAlert(Guid projectId, string projectName, NotificationRule rule, Dictionary<string, object> data)
    {
        var message = rule.MessageTemplate
            .Replace("{ProjectName}", projectName);

        // Replace data placeholders
        foreach (var kvp in data)
        {
            message = message.Replace($"{{{kvp.Key}}}", kvp.Value.ToString());
        }

        return new Alert
        {
            AlertId = Guid.NewGuid(),
            ProjectId = projectId,
            RuleId = rule.RuleId,
            RuleName = rule.Name,
            Category = rule.Category,
            Severity = rule.Severity,
            Message = message,
            CreatedDate = DateTime.Now,
            Status = AlertStatus.Active
        };
    }

    /// <summary>
    /// Sends a notification through specified channels
    /// </summary>
    public NotificationResult SendNotification(
        string title,
        string message,
        NotificationSeverity severity,
        List<NotificationChannel> channels,
        Dictionary<string, string> recipients)
    {
        var notification = new Notification
        {
            NotificationId = Guid.NewGuid(),
            Title = title,
            Message = message,
            Severity = severity,
            CreatedDate = DateTime.Now,
            Status = NotificationStatus.Pending
        };

        _notifications.Add(notification);

        var result = new NotificationResult
        {
            NotificationId = notification.NotificationId,
            IsSuccess = true
        };

        // Send through each channel
        foreach (var channel in channels)
        {
            var delivery = SendThroughChannel(channel, notification, recipients);
            notification.Deliveries.Add(delivery);

            if (delivery.IsSuccess)
            {
                result.SuccessfulChannels.Add(channel);
            }
            else
            {
                result.FailedChannels.Add(channel);
            }
        }

        notification.Status = result.FailedChannels.Any() 
            ? NotificationStatus.PartiallyDelivered 
            : NotificationStatus.Delivered;

        return result;
    }

    /// <summary>
    /// Sends notification through a specific channel
    /// </summary>
    private NotificationDelivery SendThroughChannel(
        NotificationChannel channel,
        Notification notification,
        Dictionary<string, string> recipients)
    {
        var delivery = new NotificationDelivery
        {
            Channel = channel,
            AttemptedDate = DateTime.Now
        };

        try
        {
            // Simulate channel-specific delivery
            switch (channel)
            {
                case NotificationChannel.Email:
                    if (recipients.ContainsKey("Email"))
                    {
                        // Would integrate with email service
                        delivery.IsSuccess = true;
                        delivery.DeliveredTo = recipients["Email"];
                    }
                    break;

                case NotificationChannel.InApp:
                    // In-app notification always succeeds
                    delivery.IsSuccess = true;
                    delivery.DeliveredTo = "Application";
                    break;

                case NotificationChannel.Webhook:
                    if (recipients.ContainsKey("WebhookUrl"))
                    {
                        // Would call webhook
                        delivery.IsSuccess = true;
                        delivery.DeliveredTo = recipients["WebhookUrl"];
                    }
                    break;

                case NotificationChannel.SMS:
                    if (recipients.ContainsKey("Phone"))
                    {
                        // Would integrate with SMS service
                        delivery.IsSuccess = true;
                        delivery.DeliveredTo = recipients["Phone"];
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            delivery.IsSuccess = false;
            delivery.ErrorMessage = ex.Message;
        }

        return delivery;
    }

    /// <summary>
    /// Dismisses an alert
    /// </summary>
    public bool DismissAlert(Guid alertId, string dismissedBy, string reason)
    {
        var alert = _activeAlerts.FirstOrDefault(a => a.AlertId == alertId);
        if (alert == null)
            return false;

        alert.Status = AlertStatus.Dismissed;
        alert.DismissedBy = dismissedBy;
        alert.DismissedDate = DateTime.Now;
        alert.DismissalReason = reason;

        return true;
    }

    /// <summary>
    /// Gets active alerts for a project
    /// </summary>
    public List<Alert> GetActiveAlerts(Guid projectId)
    {
        return _activeAlerts
            .Where(a => a.ProjectId == projectId && a.Status == AlertStatus.Active)
            .OrderByDescending(a => a.Severity)
            .ThenBy(a => a.CreatedDate)
            .ToList();
    }

    /// <summary>
    /// Gets alerts by severity
    /// </summary>
    public List<Alert> GetAlertsBySeverity(NotificationSeverity severity)
    {
        return _activeAlerts
            .Where(a => a.Severity == severity && a.Status == AlertStatus.Active)
            .OrderBy(a => a.CreatedDate)
            .ToList();
    }

    /// <summary>
    /// Gets notification history
    /// </summary>
    public List<Notification> GetNotificationHistory(int days = 30)
    {
        var cutoffDate = DateTime.Now.AddDays(-days);
        return _notifications
            .Where(n => n.CreatedDate >= cutoffDate)
            .OrderByDescending(n => n.CreatedDate)
            .ToList();
    }

    /// <summary>
    /// Aggregates alerts by category
    /// </summary>
    public Dictionary<NotificationCategory, int> GetAlertSummary()
    {
        return _activeAlerts
            .Where(a => a.Status == AlertStatus.Active)
            .GroupBy(a => a.Category)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}

/// <summary>
/// Notification rule
/// </summary>
public class NotificationRule
{
    public Guid RuleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public NotificationCategory Category { get; set; }
    public NotificationSeverity Severity { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Alert
/// </summary>
public class Alert
{
    public Guid AlertId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public NotificationCategory Category { get; set; }
    public NotificationSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public AlertStatus Status { get; set; }
    public string DismissedBy { get; set; } = string.Empty;
    public DateTime? DismissedDate { get; set; }
    public string DismissalReason { get; set; } = string.Empty;
}

/// <summary>
/// Notification
/// </summary>
public class Notification
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; }
    public DateTime CreatedDate { get; set; }
    public NotificationStatus Status { get; set; }
    public List<NotificationDelivery> Deliveries { get; set; } = new();
}

/// <summary>
/// Notification delivery record
/// </summary>
public class NotificationDelivery
{
    public NotificationChannel Channel { get; set; }
    public bool IsSuccess { get; set; }
    public string DeliveredTo { get; set; } = string.Empty;
    public DateTime AttemptedDate { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public enum NotificationCategory
{
    Budget,
    Deadline,
    StatusChange,
    Activity,
    Resource,
    Approval,
    System
}

public enum NotificationSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

public enum AlertStatus
{
    Active,
    Dismissed,
    Resolved,
    Expired
}

public enum NotificationStatus
{
    Pending,
    Delivered,
    PartiallyDelivered,
    Failed
}

public enum NotificationChannel
{
    Email,
    SMS,
    InApp,
    Webhook,
    Push
}

public class AlertEvaluationResult
{
    public Guid ProjectId { get; set; }
    public DateTime EvaluationDate { get; set; }
    public List<Alert> NewAlerts { get; set; } = new();
    public int TotalActiveAlerts { get; set; }
}

public class NotificationResult
{
    public Guid NotificationId { get; set; }
    public bool IsSuccess { get; set; }
    public List<NotificationChannel> SuccessfulChannels { get; set; } = new();
    public List<NotificationChannel> FailedChannels { get; set; } = new();
}
