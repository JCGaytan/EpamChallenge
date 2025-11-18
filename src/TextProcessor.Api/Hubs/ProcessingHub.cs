using Microsoft.AspNetCore.SignalR;
using TextProcessor.Core.Interfaces;
using TextProcessor.Core.Models;

namespace TextProcessor.Api.Hubs;

/// <summary>
/// SignalR hub for real-time communication with clients during text processing
/// </summary>
public class ProcessingHub : Hub
{
    private readonly ILogger<ProcessingHub> _logger;
    private readonly IJobManager _jobManager;

    public ProcessingHub(ILogger<ProcessingHub> logger, IJobManager jobManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobManager = jobManager ?? throw new ArgumentNullException(nameof(jobManager));
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var clientId = Context.ConnectionId;
        _logger.LogInformation("Client {ClientId} connected to processing hub", clientId);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Client_{clientId}");
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var clientId = Context.ConnectionId;
        _logger.LogInformation("Client {ClientId} disconnected from processing hub", clientId);
        
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client {ClientId} disconnected due to exception", clientId);
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Client_{clientId}");
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Allows a client to join a specific job group for receiving updates
    /// </summary>
    public async Task JoinJobGroup(string jobId)
    {
        if (Guid.TryParse(jobId, out var guid))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Job_{guid}");
            _logger.LogDebug("Client {ClientId} joined job group {JobId}", Context.ConnectionId, jobId);
        }
        else
        {
            _logger.LogWarning("Client {ClientId} attempted to join invalid job group {JobId}", 
                Context.ConnectionId, jobId);
        }
    }

    /// <summary>
    /// Allows a client to leave a specific job group
    /// </summary>
    public async Task LeaveJobGroup(string jobId)
    {
        if (Guid.TryParse(jobId, out var guid))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Job_{guid}");
            _logger.LogDebug("Client {ClientId} left job group {JobId}", Context.ConnectionId, jobId);
        }
    }

    /// <summary>
    /// Handles job cancellation requests from clients
    /// </summary>
    public async Task CancelJob(string jobId)
    {
        var clientId = Context.ConnectionId;
        
        if (!Guid.TryParse(jobId, out var guid))
        {
            _logger.LogWarning("Client {ClientId} attempted to cancel invalid job {JobId}", clientId, jobId);
            await Clients.Caller.SendAsync("JobCancellationFailed", new
            {
                JobId = jobId,
                Error = "Invalid job ID"
            });
            return;
        }

        try
        {
            var job = await _jobManager.GetJobAsync(guid);
            if (job == null)
            {
                _logger.LogWarning("Client {ClientId} attempted to cancel non-existent job {JobId}", clientId, jobId);
                await Clients.Caller.SendAsync("JobCancellationFailed", new
                {
                    JobId = jobId,
                    Error = "Job not found"
                });
                return;
            }

            // Verify the client owns this job (basic security check)
            if (job.ClientId != clientId)
            {
                _logger.LogWarning("Client {ClientId} attempted to cancel job {JobId} owned by {Owner}", 
                    clientId, jobId, job.ClientId);
                await Clients.Caller.SendAsync("JobCancellationFailed", new
                {
                    JobId = jobId,
                    Error = "Unauthorized"
                });
                return;
            }

            var cancelled = await _jobManager.CancelJobAsync(guid);
            if (cancelled)
            {
                _logger.LogInformation("Client {ClientId} successfully cancelled job {JobId}", clientId, jobId);
                await Clients.Group($"Job_{guid}").SendAsync("JobCancelled", new
                {
                    JobId = jobId,
                    CancelledAt = DateTime.UtcNow
                });
            }
            else
            {
                await Clients.Caller.SendAsync("JobCancellationFailed", new
                {
                    JobId = jobId,
                    Error = "Cannot cancel job in current state"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling job {JobId} for client {ClientId}", jobId, clientId);
            await Clients.Caller.SendAsync("JobCancellationFailed", new
            {
                JobId = jobId,
                Error = "Internal server error"
            });
        }
    }
}