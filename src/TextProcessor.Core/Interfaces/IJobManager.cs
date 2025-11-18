using TextProcessor.Core.Models;

namespace TextProcessor.Core.Interfaces;

/// <summary>
/// Interface for managing processing jobs lifecycle and state
/// </summary>
public interface IJobManager
{
    /// <summary>
    /// Creates a new processing job
    /// </summary>
    /// <param name="inputText">The text to process</param>
    /// <param name="clientId">Optional client identifier for tracking</param>
    /// <returns>The created job</returns>
    Task<ProcessingJob> CreateJobAsync(string inputText, string? clientId = null);

    /// <summary>
    /// Retrieves a job by its ID
    /// </summary>
    /// <param name="jobId">The job identifier</param>
    /// <returns>The job if found, null otherwise</returns>
    Task<ProcessingJob?> GetJobAsync(Guid jobId);

    /// <summary>
    /// Updates job status and metadata
    /// </summary>
    /// <param name="job">The job to update</param>
    /// <returns>Updated job</returns>
    Task<ProcessingJob> UpdateJobAsync(ProcessingJob job);

    /// <summary>
    /// Cancels a running job
    /// </summary>
    /// <param name="jobId">The job identifier</param>
    /// <returns>True if cancelled successfully</returns>
    Task<bool> CancelJobAsync(Guid jobId);

    /// <summary>
    /// Gets all jobs for a specific client
    /// </summary>
    /// <param name="clientId">The client identifier</param>
    /// <returns>List of jobs</returns>
    Task<IEnumerable<ProcessingJob>> GetJobsByClientAsync(string clientId);

    /// <summary>
    /// Cleans up old completed or failed jobs
    /// </summary>
    /// <param name="olderThan">Remove jobs older than this timespan</param>
    /// <returns>Number of cleaned up jobs</returns>
    Task<int> CleanupOldJobsAsync(TimeSpan olderThan);
}