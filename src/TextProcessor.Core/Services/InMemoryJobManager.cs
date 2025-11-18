using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TextProcessor.Core.Interfaces;
using TextProcessor.Core.Models;

namespace TextProcessor.Core.Services;

/// <summary>
/// In-memory job manager for development and testing. 
/// In production, this would typically use a persistent storage like SQL Server or Redis.
/// </summary>
public class InMemoryJobManager : IJobManager
{
    private readonly ILogger<InMemoryJobManager> _logger;
    private readonly ConcurrentDictionary<Guid, ProcessingJob> _jobs;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens;

    public InMemoryJobManager(ILogger<InMemoryJobManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobs = new ConcurrentDictionary<Guid, ProcessingJob>();
        _cancellationTokens = new ConcurrentDictionary<Guid, CancellationTokenSource>();
    }

    public Task<ProcessingJob> CreateJobAsync(string inputText, string? clientId = null)
    {
        if (string.IsNullOrWhiteSpace(inputText))
        {
            throw new ArgumentException("Input text cannot be null or empty", nameof(inputText));
        }

        var job = new ProcessingJob
        {
            Id = Guid.NewGuid(),
            InputText = inputText,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ClientId = clientId,
            TotalCharacters = CalculateExpectedOutputLength(inputText)
        };

        _jobs.TryAdd(job.Id, job);
        _cancellationTokens.TryAdd(job.Id, new CancellationTokenSource());

        _logger.LogInformation("Created new job {JobId} for client {ClientId} with {Length} characters", 
            job.Id, clientId ?? "unknown", inputText.Length);

        return Task.FromResult(job);
    }

    public Task<ProcessingJob?> GetJobAsync(Guid jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<ProcessingJob> UpdateJobAsync(ProcessingJob job)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        _jobs.TryUpdate(job.Id, job, _jobs[job.Id]);
        
        _logger.LogDebug("Updated job {JobId} status to {Status}", job.Id, job.Status);
        
        return Task.FromResult(job);
    }

    public Task<bool> CancelJobAsync(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            _logger.LogWarning("Attempted to cancel non-existent job {JobId}", jobId);
            return Task.FromResult(false);
        }

        if (!job.CanBeCancelled)
        {
            _logger.LogWarning("Cannot cancel job {JobId} in status {Status}", jobId, job.Status);
            return Task.FromResult(false);
        }

        // Cancel the associated token
        if (_cancellationTokens.TryGetValue(jobId, out var cts))
        {
            try
            {
                cts.Cancel();
                _logger.LogInformation("Cancelled cancellation token for job {JobId}", jobId);
            }
            catch (ObjectDisposedException)
            {
                _logger.LogDebug("Cancellation token for job {JobId} was already disposed", jobId);
            }
        }

        // Update job status
        job.Status = JobStatus.Cancelled;
        job.CompletedAt = DateTime.UtcNow;
        
        _jobs.TryUpdate(jobId, job, job);

        _logger.LogInformation("Successfully cancelled job {JobId}", jobId);
        return Task.FromResult(true);
    }

    public Task<IEnumerable<ProcessingJob>> GetJobsByClientAsync(string clientId)
    {
        var jobs = _jobs.Values
            .Where(job => job.ClientId == clientId)
            .OrderByDescending(job => job.CreatedAt)
            .ToList();

        _logger.LogDebug("Found {Count} jobs for client {ClientId}", jobs.Count, clientId);
        
        return Task.FromResult<IEnumerable<ProcessingJob>>(jobs);
    }

    public Task<int> CleanupOldJobsAsync(TimeSpan olderThan)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(olderThan);
        var jobsToRemove = _jobs.Values
            .Where(job => job.CreatedAt < cutoffTime && 
                         (job.Status == JobStatus.Completed || 
                          job.Status == JobStatus.Failed || 
                          job.Status == JobStatus.Cancelled))
            .ToList();

        int removedCount = 0;
        foreach (var job in jobsToRemove)
        {
            if (_jobs.TryRemove(job.Id, out _))
            {
                // Also clean up cancellation tokens
                if (_cancellationTokens.TryRemove(job.Id, out var cts))
                {
                    cts.Dispose();
                }
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} old jobs older than {TimeSpan}", 
                removedCount, olderThan);
        }

        return Task.FromResult(removedCount);
    }

    /// <summary>
    /// Gets the cancellation token for a specific job
    /// </summary>
    public CancellationToken GetCancellationToken(Guid jobId)
    {
        return _cancellationTokens.TryGetValue(jobId, out var cts) 
            ? cts.Token 
            : CancellationToken.None;
    }

    /// <summary>
    /// Calculates the expected length of the output string
    /// </summary>
    private static int CalculateExpectedOutputLength(string input)
    {
        // Character analysis part
        var uniqueChars = input.Distinct().Count();
        var charAnalysisLength = uniqueChars * 2; // Each char + its count (approximation)
        
        // Base64 encoded part
        var base64Length = ((input.Length + 2) / 3) * 4;
        
        // Slash separator
        var separatorLength = 1;
        
        return charAnalysisLength + separatorLength + base64Length;
    }
}