using Microsoft.AspNetCore.SignalR;
using TextProcessor.Api.Hubs;
using TextProcessor.Core.Interfaces;
using TextProcessor.Core.Models;

namespace TextProcessor.Api.Services;

/// <summary>
/// SignalR-based real-time notification service for job progress and status updates
/// </summary>
public class SignalRNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<ProcessingHub> _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<ProcessingHub> hubContext, 
        ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyCharacterProcessedAsync(string clientId, Guid jobId, char character, double progress)
    {
        try
        {
            await _hubContext.Clients.Group($"Job_{jobId}")
                .SendAsync("CharacterProcessed", new
                {
                    JobId = jobId,
                    Character = character,
                    Progress = Math.Round(progress, 2)
                });

            _logger.LogDebug("Notified character '{Character}' processed for job {JobId}, progress: {Progress}%", 
                character, jobId, Math.Round(progress, 2));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify character processed for job {JobId}", jobId);
        }
    }

    public async Task NotifyJobCompletedAsync(string clientId, ProcessingJob job)
    {
        try
        {
            await _hubContext.Clients.Group($"Job_{job.Id}")
                .SendAsync("JobCompleted", new
                {
                    JobId = job.Id,
                    Result = job.ProcessedText,
                    CompletedAt = job.CompletedAt,
                    Duration = job.CompletedAt - job.StartedAt
                });

            _logger.LogInformation("Notified job completion for {JobId}", job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify job completion for job {JobId}", job.Id);
        }
    }

    public async Task NotifyJobCancelledAsync(string clientId, Guid jobId)
    {
        try
        {
            await _hubContext.Clients.Group($"Job_{jobId}")
                .SendAsync("JobCancelled", new
                {
                    JobId = jobId,
                    CancelledAt = DateTime.UtcNow
                });

            _logger.LogInformation("Notified job cancellation for {JobId}", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify job cancellation for job {JobId}", jobId);
        }
    }

    public async Task NotifyJobFailedAsync(string clientId, Guid jobId, string errorMessage)
    {
        try
        {
            await _hubContext.Clients.Group($"Job_{jobId}")
                .SendAsync("JobFailed", new
                {
                    JobId = jobId,
                    ErrorMessage = errorMessage,
                    FailedAt = DateTime.UtcNow
                });

            _logger.LogWarning("Notified job failure for {JobId}: {ErrorMessage}", jobId, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify job failure for job {JobId}", jobId);
        }
    }
}