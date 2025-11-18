using TextProcessor.Core.Models;

namespace TextProcessor.Core.Interfaces;

/// <summary>
/// Interface for background job processing with proper cancellation support
/// </summary>
public interface IBackgroundJobProcessor
{
    /// <summary>
    /// Schedules a job for background processing
    /// </summary>
    /// <param name="jobId">The job identifier</param>
    /// <returns>Background job identifier</returns>
    Task<string> ScheduleJobAsync(Guid jobId);

    /// <summary>
    /// Cancels a background job
    /// </summary>
    /// <param name="jobId">The job identifier</param>
    /// <returns>True if cancelled successfully</returns>
    Task<bool> CancelBackgroundJobAsync(Guid jobId);

    /// <summary>
    /// Gets the status of a background job
    /// </summary>
    /// <param name="jobId">The job identifier</param>
    /// <returns>Job status information</returns>
    Task<JobStatus> GetJobStatusAsync(Guid jobId);
}

/// <summary>
/// Interface for real-time communication with clients
/// </summary>
public interface IRealtimeNotificationService
{
    /// <summary>
    /// Notifies client about job progress
    /// </summary>
    /// <param name="clientId">The client identifier</param>
    /// <param name="jobId">The job identifier</param>
    /// <param name="character">The processed character</param>
    /// <param name="progress">Current progress percentage</param>
    Task NotifyCharacterProcessedAsync(string clientId, Guid jobId, char character, double progress);

    /// <summary>
    /// Notifies client about job completion
    /// </summary>
    /// <param name="clientId">The client identifier</param>
    /// <param name="job">The completed job</param>
    Task NotifyJobCompletedAsync(string clientId, ProcessingJob job);

    /// <summary>
    /// Notifies client about job cancellation
    /// </summary>
    /// <param name="clientId">The client identifier</param>
    /// <param name="jobId">The job identifier</param>
    Task NotifyJobCancelledAsync(string clientId, Guid jobId);

    /// <summary>
    /// Notifies client about job failure
    /// </summary>
    /// <param name="clientId">The client identifier</param>
    /// <param name="jobId">The job identifier</param>
    /// <param name="errorMessage">The error message</param>
    Task NotifyJobFailedAsync(string clientId, Guid jobId, string errorMessage);
}