using TextProcessor.Core.Interfaces;
using TextProcessor.Core.Models;

namespace TextProcessor.Api.Services;

/// <summary>
/// Built-in background job processor using ASP.NET Core BackgroundService
/// </summary>
public class BuiltInJobProcessor : IBackgroundJobProcessor
{
    private readonly ILogger<BuiltInJobProcessor> _logger;
    private readonly BackgroundJobService _backgroundJobService;

    public BuiltInJobProcessor(
        ILogger<BuiltInJobProcessor> logger,
        BackgroundJobService backgroundJobService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backgroundJobService = backgroundJobService ?? throw new ArgumentNullException(nameof(backgroundJobService));
    }

    public Task<string> ScheduleJobAsync(Guid jobId)
    {
        try
        {
            _backgroundJobService.EnqueueJob(jobId);
            
            _logger.LogInformation("Scheduled background job for processing job {JobId}", jobId);
            
            return Task.FromResult(jobId.ToString()); // Return job ID as the "background job ID"
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule background job for {JobId}", jobId);
            throw;
        }
    }

    public Task<bool> CancelBackgroundJobAsync(Guid jobId)
    {
        try
        {
            var cancelled = _backgroundJobService.CancelJob(jobId);
            _logger.LogInformation("Cancel job {JobId} result: {Cancelled}", jobId, cancelled);
            return Task.FromResult(cancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel background job for {JobId}", jobId);
            return Task.FromResult(false);
        }
    }

    public Task<JobStatus> GetJobStatusAsync(Guid jobId)
    {
        // This method isn't used much since we rely on the job manager for status
        return Task.FromResult(JobStatus.Running); // Default status for background jobs
    }
}