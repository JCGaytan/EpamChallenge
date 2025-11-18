using Microsoft.Extensions.Diagnostics.HealthChecks;
using TextProcessor.Core.Interfaces;

namespace TextProcessor.Api.HealthChecks;

/// <summary>
/// Health check for text processing service availability and performance
/// </summary>
public class TextProcessingHealthCheck : IHealthCheck
{
    private readonly ITextProcessingService _textProcessingService;
    private readonly ILogger<TextProcessingHealthCheck> _logger;

    public TextProcessingHealthCheck(
        ITextProcessingService textProcessingService,
        ILogger<TextProcessingHealthCheck> logger)
    {
        _textProcessingService = textProcessingService ?? throw new ArgumentNullException(nameof(textProcessingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            
            // Test basic functionality
            var testResult = _textProcessingService.AnalyzeCharacterFrequency("test");
            var base64Result = _textProcessingService.EncodeToBase64("test");
            
            var endTime = DateTime.UtcNow;
            var responseTime = endTime - startTime;

            // Check if results are correct
            if (testResult.Count != 3 || string.IsNullOrEmpty(base64Result))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Text processing service returned unexpected results",
                    data: new Dictionary<string, object>
                    {
                        ["ResponseTime"] = responseTime.TotalMilliseconds,
                        ["TestResultCount"] = testResult.Count,
                        ["Base64Result"] = base64Result ?? "null"
                    }));
            }

            // Check performance
            if (responseTime.TotalMilliseconds > 1000) // 1 second threshold
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    "Text processing service is responding slowly",
                    data: new Dictionary<string, object>
                    {
                        ["ResponseTime"] = responseTime.TotalMilliseconds,
                        ["Threshold"] = 1000
                    }));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "Text processing service is working correctly",
                data: new Dictionary<string, object>
                {
                    ["ResponseTime"] = responseTime.TotalMilliseconds,
                    ["TestResultCount"] = testResult.Count,
                    ["Base64Length"] = base64Result.Length
                }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for text processing service");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Text processing service is not available",
                ex,
                data: new Dictionary<string, object>
                {
                    ["Error"] = ex.Message
                }));
        }
    }
}

/// <summary>
/// Health check for job manager functionality
/// </summary>
public class JobManagerHealthCheck : IHealthCheck
{
    private readonly IJobManager _jobManager;
    private readonly ILogger<JobManagerHealthCheck> _logger;

    public JobManagerHealthCheck(
        IJobManager jobManager,
        ILogger<JobManagerHealthCheck> logger)
    {
        _jobManager = jobManager ?? throw new ArgumentNullException(nameof(jobManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            
            // Test job creation and retrieval
            var testJob = await _jobManager.CreateJobAsync("health-check", "health-check-client");
            var retrievedJob = await _jobManager.GetJobAsync(testJob.Id);
            
            var endTime = DateTime.UtcNow;
            var responseTime = endTime - startTime;

            if (retrievedJob == null || retrievedJob.Id != testJob.Id)
            {
                return HealthCheckResult.Unhealthy(
                    "Job manager cannot create or retrieve jobs properly",
                    data: new Dictionary<string, object>
                    {
                        ["ResponseTime"] = responseTime.TotalMilliseconds,
                        ["TestJobId"] = testJob.Id,
                        ["RetrievedJobId"] = retrievedJob?.Id.ToString() ?? "null"
                    });
            }

            // Clean up test job
            await _jobManager.CancelJobAsync(testJob.Id);

            return HealthCheckResult.Healthy(
                "Job manager is working correctly",
                data: new Dictionary<string, object>
                {
                    ["ResponseTime"] = responseTime.TotalMilliseconds,
                    ["TestJobId"] = testJob.Id
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for job manager");
            return HealthCheckResult.Unhealthy(
                "Job manager is not available",
                ex,
                data: new Dictionary<string, object>
                {
                    ["Error"] = ex.Message
                });
        }
    }
}