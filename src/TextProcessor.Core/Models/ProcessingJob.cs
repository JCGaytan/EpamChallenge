using System.ComponentModel.DataAnnotations;

namespace TextProcessor.Core.Models;

/// <summary>
/// Represents a text processing job with its current state and metadata
/// </summary>
public class ProcessingJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InputText { get; set; } = string.Empty;
    public string? ProcessedText { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalCharacters { get; set; }
    public int ProcessedCharacters { get; set; }
    public string? ClientId { get; set; }

    /// <summary>
    /// Calculates the progress percentage of the job
    /// </summary>
    public double ProgressPercentage => TotalCharacters == 0 ? 0 : (double)ProcessedCharacters / TotalCharacters * 100;

    /// <summary>
    /// Indicates whether the job can be cancelled
    /// </summary>
    public bool CanBeCancelled => Status == JobStatus.Running || Status == JobStatus.Pending;

    /// <summary>
    /// Indicates whether the job is in a final state
    /// </summary>
    public bool IsFinished => Status == JobStatus.Completed || Status == JobStatus.Cancelled || Status == JobStatus.Failed;
}

/// <summary>
/// Enumeration of possible job statuses
/// </summary>
public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Cancelled = 3,
    Failed = 4
}