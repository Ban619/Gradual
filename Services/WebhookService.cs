using System.Net.Http;
using System.Text;
using System.Text.Json;
using Gradual.Models;

namespace Gradual.Services;

/// <summary>
/// Service for sending webhook notifications to external systems
/// </summary>
public class WebhookService
{
    private readonly ConfigurationService _configService;
    private readonly LoggingService _loggingService;
    private readonly HttpClient _httpClient;
    private readonly bool _enabled;

    public WebhookService(ConfigurationService configService, LoggingService loggingService)
    {
        _configService = configService;
        _loggingService = loggingService;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _enabled = configService.Settings.Webhooks.Enabled;
    }

    /// <summary>
    /// Send webhook notification for project created event
    /// </summary>
    public async Task NotifyProjectCreatedAsync(ProjectRecord project)
    {
        await SendWebhookAsync("ProjectCreated", new
        {
            EventType = "ProjectCreated",
            Timestamp = DateTime.UtcNow,
            Data = new
            {
                project.Id,
                project.ProjectName,
                project.Client,
                project.Status,
                project.Priority,
                project.CreatedAt
            }
        });
    }

    /// <summary>
    /// Send webhook notification for project updated event
    /// </summary>
    public async Task NotifyProjectUpdatedAsync(ProjectRecord project)
    {
        await SendWebhookAsync("ProjectUpdated", new
        {
            EventType = "ProjectUpdated",
            Timestamp = DateTime.UtcNow,
            Data = new
            {
                project.Id,
                project.ProjectName,
                project.Client,
                project.Status,
                project.Priority,
                project.UpdatedAt
            }
        });
    }

    /// <summary>
    /// Send webhook notification for project deleted event
    /// </summary>
    public async Task NotifyProjectDeletedAsync(Guid projectId, string projectName)
    {
        await SendWebhookAsync("ProjectDeleted", new
        {
            EventType = "ProjectDeleted",
            Timestamp = DateTime.UtcNow,
            Data = new
            {
                Id = projectId,
                ProjectName = projectName
            }
        });
    }

    /// <summary>
    /// Send custom webhook notification
    /// </summary>
    public async Task SendCustomWebhookAsync(string eventName, object data)
    {
        await SendWebhookAsync(eventName, new
        {
            EventType = eventName,
            Timestamp = DateTime.UtcNow,
            Data = data
        });
    }

    private async Task SendWebhookAsync(string eventName, object payload)
    {
        if (!_enabled)
        {
            _loggingService.LogDebug("Webhooks disabled. Skipping: {EventName}", eventName);
            return;
        }

        var endpoints = _configService.Settings.Webhooks.Endpoints
            .Where(e => e.Name == eventName && e.Enabled && !string.IsNullOrWhiteSpace(e.Url))
            .ToList();

        if (endpoints.Count == 0)
        {
            _loggingService.LogDebug("No enabled webhook endpoints for event: {EventName}", eventName);
            return;
        }

        foreach (var endpoint in endpoints)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    _loggingService.LogInformation("Sending webhook to {Url} for event {EventName}", endpoint.Url, eventName);

                    var response = await _httpClient.PostAsync(endpoint.Url, content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        _loggingService.LogInformation("Webhook sent successfully to {Url}", endpoint.Url);
                    }
                    else
                    {
                        _loggingService.LogWarning("Webhook failed with status {StatusCode} for {Url}", 
                            response.StatusCode, endpoint.Url);
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError(ex, "Failed to send webhook to {Url} for event {EventName}", 
                        endpoint.Url, eventName);
                }
            });
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
