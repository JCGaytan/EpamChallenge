using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TextProcessor.Api.Services;

namespace TextProcessor.Api.Controllers;

/// <summary>
/// Controller for exposing application metrics and monitoring endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMetricsService metricsService,
        ILogger<MetricsController> logger)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets current application metrics
    /// </summary>
    /// <returns>Application metrics data</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApplicationMetrics), StatusCodes.Status200OK)]
    public IActionResult GetMetrics()
    {
        try
        {
            var metrics = _metricsService.GetMetrics();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve application metrics");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { Error = "Failed to retrieve metrics" });
        }
    }

    /// <summary>
    /// Gets metrics in Prometheus format for monitoring integration
    /// </summary>
    /// <returns>Metrics in Prometheus format</returns>
    [HttpGet("prometheus")]
    [Produces("text/plain")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetPrometheusMetrics()
    {
        try
        {
            var metrics = _metricsService.GetMetrics();
            var prometheusFormat = FormatPrometheusMetrics(metrics);
            return Content(prometheusFormat, "text/plain");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve Prometheus metrics");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve metrics");
        }
    }

    /// <summary>
    /// Gets system information and runtime statistics
    /// </summary>
    /// <returns>System information</returns>
    [HttpGet("system")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSystemInfo()
    {
        try
        {
            var systemInfo = new
            {
                Environment = Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .Where(e => e.Key.ToString()!.StartsWith("ASPNETCORE_") || 
                               e.Key.ToString()!.StartsWith("DOTNET_"))
                    .ToDictionary(e => e.Key.ToString()!, e => e.Value?.ToString()),
                Runtime = new
                {
                    Version = Environment.Version.ToString(),
                    Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = Environment.WorkingSet,
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    UpTime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
                },
                Memory = new
                {
                    WorkingSet = GC.GetTotalMemory(false),
                    Generation0Collections = GC.CollectionCount(0),
                    Generation1Collections = GC.CollectionCount(1),
                    Generation2Collections = GC.CollectionCount(2)
                }
            };

            return Ok(systemInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve system information");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { Error = "Failed to retrieve system information" });
        }
    }

    private static string FormatPrometheusMetrics(ApplicationMetrics metrics)
    {
        var sb = new System.Text.StringBuilder();

        // Job metrics
        sb.AppendLine("# HELP textprocessor_jobs_started_total Total number of jobs started");
        sb.AppendLine("# TYPE textprocessor_jobs_started_total counter");
        sb.AppendLine($"textprocessor_jobs_started_total {metrics.JobsStarted}");
        
        sb.AppendLine("# HELP textprocessor_jobs_completed_total Total number of jobs completed");
        sb.AppendLine("# TYPE textprocessor_jobs_completed_total counter");
        sb.AppendLine($"textprocessor_jobs_completed_total {metrics.JobsCompleted}");
        
        sb.AppendLine("# HELP textprocessor_jobs_cancelled_total Total number of jobs cancelled");
        sb.AppendLine("# TYPE textprocessor_jobs_cancelled_total counter");
        sb.AppendLine($"textprocessor_jobs_cancelled_total {metrics.JobsCancelled}");
        
        sb.AppendLine("# HELP textprocessor_jobs_failed_total Total number of jobs failed");
        sb.AppendLine("# TYPE textprocessor_jobs_failed_total counter");
        sb.AppendLine($"textprocessor_jobs_failed_total {metrics.JobsFailed}");

        // Character metrics
        sb.AppendLine("# HELP textprocessor_characters_processed_total Total number of characters processed");
        sb.AppendLine("# TYPE textprocessor_characters_processed_total counter");
        sb.AppendLine($"textprocessor_characters_processed_total {metrics.TotalCharactersProcessed}");

        // Duration metrics
        sb.AppendLine("# HELP textprocessor_job_duration_average_ms Average job duration in milliseconds");
        sb.AppendLine("# TYPE textprocessor_job_duration_average_ms gauge");
        sb.AppendLine($"textprocessor_job_duration_average_ms {metrics.AverageJobDurationMs:F2}");
        
        sb.AppendLine("# HELP textprocessor_job_duration_max_ms Maximum job duration in milliseconds");
        sb.AppendLine("# TYPE textprocessor_job_duration_max_ms gauge");
        sb.AppendLine($"textprocessor_job_duration_max_ms {metrics.MaxJobDurationMs:F2}");

        // API metrics
        foreach (var kvp in metrics.ApiRequestCounts)
        {
            var endpoint = kvp.Key.Replace("\"", "\\\"");
            sb.AppendLine($"textprocessor_api_requests_total{{endpoint=\"{endpoint}\"}} {kvp.Value}");
        }

        foreach (var kvp in metrics.ApiAverageResponseTimes)
        {
            var endpoint = kvp.Key.Replace("\"", "\\\"");
            sb.AppendLine($"textprocessor_api_response_time_average_ms{{endpoint=\"{endpoint}\"}} {kvp.Value:F2}");
        }

        return sb.ToString();
    }
}