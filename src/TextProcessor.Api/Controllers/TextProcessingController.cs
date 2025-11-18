using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using TextProcessor.Core.Interfaces;
using TextProcessor.Core.Models;

namespace TextProcessor.Api.Controllers;

/// <summary>
/// API controller for text processing operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TextProcessingController : ControllerBase
{
    private readonly ILogger<TextProcessingController> _logger;
    private readonly IJobManager _jobManager;
    private readonly IBackgroundJobProcessor _jobProcessor;
    private const string SignalRConnectionHeaderName = "X-SignalR-ConnectionId";

    public TextProcessingController(
        ILogger<TextProcessingController> logger,
        IJobManager jobManager,
        IBackgroundJobProcessor jobProcessor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jobManager = jobManager ?? throw new ArgumentNullException(nameof(jobManager));
        _jobProcessor = jobProcessor ?? throw new ArgumentNullException(nameof(jobProcessor));
    }

    /// <summary>
    /// Starts a new text processing job
    /// </summary>
    /// <param name="request">The text processing request</param>
    /// <returns>Information about the created job</returns>
    [HttpPost("process")]
    [ProducesResponseType(typeof(ProcessingJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessText([FromBody] ProcessTextRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Prefer the SignalR connection id supplied by the client so we can authorize hub actions
            var clientId = ResolveClientId() ?? Guid.NewGuid().ToString();
            
            // Create the processing job
            var job = await _jobManager.CreateJobAsync(request.Text, clientId);
            
            // Schedule the background job
            var backgroundJobId = await _jobProcessor.ScheduleJobAsync(job.Id);
            
            _logger.LogInformation("Created and scheduled processing job {JobId} for client {ClientId}",
                job.Id, clientId);

            var dto = MapToDto(job);
            return CreatedAtAction(nameof(GetJob), new { jobId = job.Id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process text request");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { Error = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Gets the status and details of a processing job
    /// </summary>
    /// <param name="jobId">The job identifier</param>
    /// <returns>Job information</returns>
    [HttpGet("jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(ProcessingJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(Guid jobId)
    {
        try
        {
            var job = await _jobManager.GetJobAsync(jobId);
            if (job == null)
            {
                return NotFound(new { Error = "Job not found" });
            }

            var dto = MapToDto(job);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job {JobId}", jobId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { Error = "An error occurred while retrieving the job" });
        }
    }

    /// <summary>
    /// Cancels a running processing job
    /// </summary>
    /// <param name="jobId">The job identifier</param>
    /// <returns>Cancellation result</returns>
    [HttpPost("jobs/{jobId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelJob(Guid jobId)
    {
        try
        {
            var job = await _jobManager.GetJobAsync(jobId);
            if (job == null)
            {
                return NotFound(new { Error = "Job not found" });
            }

            if (!job.CanBeCancelled)
            {
                return BadRequest(new { Error = $"Job cannot be cancelled in status: {job.Status}" });
            }

            var cancelled = await _jobProcessor.CancelBackgroundJobAsync(jobId);
            if (cancelled)
            {
                _logger.LogInformation("Successfully cancelled job {JobId}", jobId);
                return Ok(new { Message = "Job cancelled successfully" });
            }
            else
            {
                return BadRequest(new { Error = "Failed to cancel job" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job {JobId}", jobId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { Error = "An error occurred while cancelling the job" });
        }
    }

    /// <summary>
    /// Gets all jobs for the current client
    /// </summary>
    /// <returns>List of jobs</returns>
    [HttpGet("jobs")]
    [ProducesResponseType(typeof(IEnumerable<ProcessingJobDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobs()
    {
        try
        {
            var clientId = ResolveClientId() ?? HttpContext.Connection.Id ?? string.Empty;
            var jobs = await _jobManager.GetJobsByClientAsync(clientId);
            
            var dtos = jobs.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get jobs for client");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { Error = "An error occurred while retrieving jobs" });
        }
    }

    private static ProcessingJobDto MapToDto(ProcessingJob job)
    {
        return new ProcessingJobDto
        {
            Id = job.Id,
            InputText = job.InputText,
            ProcessedText = job.ProcessedText,
            Status = job.Status.ToString(),
            Progress = job.ProgressPercentage,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            ErrorMessage = job.ErrorMessage
        };
    }

    private string? ResolveClientId()
    {
        if (Request.Headers.TryGetValue(SignalRConnectionHeaderName, out var headerValue))
        {
            var connectionId = headerValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(connectionId))
            {
                return connectionId;
            }
        }

        return HttpContext.Connection.Id;
    }
}

/// <summary>
/// Request model for text processing
/// </summary>
public class ProcessTextRequest
{
    /// <summary>
    /// The text to process
    /// </summary>
    [Required(ErrorMessage = "Text is required")]
    [StringLength(10000, ErrorMessage = "Text cannot exceed 10,000 characters")]
    [MinLength(1, ErrorMessage = "Text cannot be empty")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// DTO for processing job information
/// </summary>
public class ProcessingJobDto
{
    public Guid Id { get; set; }
    public string InputText { get; set; } = string.Empty;
    public string? ProcessedText { get; set; }
    public string Status { get; set; } = string.Empty;
    public double Progress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}