using System.Collections.Concurrent;

namespace TextProcessor.Api.Services;

/// <summary>
/// Service for collecting and exposing application metrics
/// </summary>
public interface IMetricsService
{
    void IncrementJobsStarted();
    void IncrementJobsCompleted();
    void IncrementJobsCancelled();
    void IncrementJobsFailed();
    void RecordJobDuration(TimeSpan duration);
    void RecordCharactersProcessed(int count);
    void IncrementApiRequests(string endpoint);
    void RecordApiResponseTime(string endpoint, TimeSpan responseTime);
    ApplicationMetrics GetMetrics();
}

public class ApplicationMetrics
{
    public long JobsStarted { get; set; }
    public long JobsCompleted { get; set; }
    public long JobsCancelled { get; set; }
    public long JobsFailed { get; set; }
    public long TotalCharactersProcessed { get; set; }
    public double AverageJobDurationMs { get; set; }
    public double MaxJobDurationMs { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, long> ApiRequestCounts { get; set; } = new();
    public Dictionary<string, double> ApiAverageResponseTimes { get; set; } = new();
}

/// <summary>
/// In-memory metrics service for collecting application performance data
/// </summary>
public class InMemoryMetricsService : IMetricsService
{
    private readonly ConcurrentBag<TimeSpan> _jobDurations = new();
    private readonly ConcurrentDictionary<string, long> _apiRequestCounts = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<TimeSpan>> _apiResponseTimes = new();
    
    private long _jobsStarted = 0;
    private long _jobsCompleted = 0;
    private long _jobsCancelled = 0;
    private long _jobsFailed = 0;
    private long _charactersProcessed = 0;

    public void IncrementJobsStarted() => Interlocked.Increment(ref _jobsStarted);
    public void IncrementJobsCompleted() => Interlocked.Increment(ref _jobsCompleted);
    public void IncrementJobsCancelled() => Interlocked.Increment(ref _jobsCancelled);
    public void IncrementJobsFailed() => Interlocked.Increment(ref _jobsFailed);

    public void RecordJobDuration(TimeSpan duration)
    {
        _jobDurations.Add(duration);
    }

    public void RecordCharactersProcessed(int count)
    {
        Interlocked.Add(ref _charactersProcessed, count);
    }

    public void IncrementApiRequests(string endpoint)
    {
        _apiRequestCounts.AddOrUpdate(endpoint, 1, (key, value) => value + 1);
    }

    public void RecordApiResponseTime(string endpoint, TimeSpan responseTime)
    {
        _apiResponseTimes.AddOrUpdate(
            endpoint,
            new ConcurrentBag<TimeSpan> { responseTime },
            (key, bag) => { bag.Add(responseTime); return bag; });
    }

    public ApplicationMetrics GetMetrics()
    {
        var durations = _jobDurations.ToArray();
        
        return new ApplicationMetrics
        {
            JobsStarted = _jobsStarted,
            JobsCompleted = _jobsCompleted,
            JobsCancelled = _jobsCancelled,
            JobsFailed = _jobsFailed,
            TotalCharactersProcessed = _charactersProcessed,
            AverageJobDurationMs = durations.Length > 0 ? durations.Average(d => d.TotalMilliseconds) : 0,
            MaxJobDurationMs = durations.Length > 0 ? durations.Max(d => d.TotalMilliseconds) : 0,
            LastUpdated = DateTime.UtcNow,
            ApiRequestCounts = new Dictionary<string, long>(_apiRequestCounts),
            ApiAverageResponseTimes = _apiResponseTimes.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Count > 0 ? kvp.Value.Average(t => t.TotalMilliseconds) : 0)
        };
    }
}

/// <summary>
/// Middleware for collecting API metrics
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMetricsService _metricsService;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(
        RequestDelegate next,
        IMetricsService metricsService,
        ILogger<MetricsMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        var endpoint = $"{context.Request.Method} {context.Request.Path}";

        try
        {
            await _next(context);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _metricsService.IncrementApiRequests(endpoint);
            _metricsService.RecordApiResponseTime(endpoint, duration);
        }
    }
}