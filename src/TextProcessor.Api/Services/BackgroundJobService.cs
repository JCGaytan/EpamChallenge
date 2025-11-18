using System.Collections.Concurrent;
using TextProcessor.Core.Interfaces;
using TextProcessor.Core.Models;

namespace TextProcessor.Api.Services;

/// <summary>
/// Built-in background service for processing jobs without external dependencies
/// </summary>
public class BackgroundJobService : BackgroundService
{
    private readonly ILogger<BackgroundJobService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentQueue<Guid> _jobQueue = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _jobCancellations = new();

    public BackgroundJobService(
        ILogger<BackgroundJobService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public void EnqueueJob(Guid jobId)
    {
        _jobQueue.Enqueue(jobId);
        _logger.LogInformation("Enqueued job {JobId} for background processing", jobId);
    }

    public bool CancelJob(Guid jobId)
    {
        if (_jobCancellations.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancelled job {JobId}", jobId);
            return true;
        }
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Job Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_jobQueue.TryDequeue(out var jobId))
                {
                    await ProcessJobAsync(jobId, stoppingToken);
                }
                else
                {
                    // Wait a bit if no jobs are available
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background job processing loop");
                await Task.Delay(1000, stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation("Background Job Service stopped");
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var jobManager = scope.ServiceProvider.GetRequiredService<IJobManager>();
        var textProcessingService = scope.ServiceProvider.GetRequiredService<ITextProcessingService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IRealtimeNotificationService>();

        ProcessingJob? job = null;
        var jobCts = new CancellationTokenSource();
        _jobCancellations[jobId] = jobCts;

        try
        {
            _logger.LogInformation("Starting background processing for job {JobId}", jobId);

            job = await jobManager.GetJobAsync(jobId);
            if (job == null)
            {
                _logger.LogError("Job {JobId} not found for processing", jobId);
                return;
            }

            // Update job status to running
            job.Status = JobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            await jobManager.UpdateJobAsync(job);

            // Get cancellation token from job manager (if using InMemoryJobManager)
            var jobCancellationToken = CancellationToken.None;
            if (jobManager is TextProcessor.Core.Services.InMemoryJobManager inMemoryManager)
            {
                jobCancellationToken = inMemoryManager.GetCancellationToken(jobId);
            }

            // Combine all cancellation tokens
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken, jobCts.Token, jobCancellationToken);

            // Process the text with character-by-character notifications
            var result = await textProcessingService.ProcessTextAsync(
                job.InputText,
                async (eventArgs) =>
                {
                    // Update job progress
                    job.ProcessedCharacters = eventArgs.Position + 1;
                    await jobManager.UpdateJobAsync(job);

                    // Notify client about character processing
                    await notificationService.NotifyCharacterProcessedAsync(
                        job.ClientId ?? string.Empty,
                        jobId,
                        eventArgs.Character,
                        job.ProgressPercentage);
                },
                combinedCts.Token);

            // Update job with results
            job.ProcessedText = result.FormattedResult;
            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            await jobManager.UpdateJobAsync(job);

            // Notify client about completion
            await notificationService.NotifyJobCompletedAsync(
                job.ClientId ?? string.Empty, job);

            _logger.LogInformation("Successfully completed processing for job {JobId}", jobId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing cancelled for job {JobId}", jobId);

            if (job != null)
            {
                job.Status = JobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
                await jobManager.UpdateJobAsync(job);

                await notificationService.NotifyJobCancelledAsync(
                    job.ClientId ?? string.Empty, jobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", jobId);

            if (job != null)
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                await jobManager.UpdateJobAsync(job);

                await notificationService.NotifyJobFailedAsync(
                    job.ClientId ?? string.Empty, jobId, ex.Message);
            }
        }
        finally
        {
            _jobCancellations.TryRemove(jobId, out _);
            jobCts?.Dispose();
        }
    }
}