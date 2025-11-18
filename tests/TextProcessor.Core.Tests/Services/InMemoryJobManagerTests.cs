using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TextProcessor.Core.Models;
using TextProcessor.Core.Services;

namespace TextProcessor.Core.Tests.Services;

public class InMemoryJobManagerTests
{
    private readonly Mock<ILogger<InMemoryJobManager>> _mockLogger;
    private readonly InMemoryJobManager _jobManager;

    public InMemoryJobManagerTests()
    {
        _mockLogger = new Mock<ILogger<InMemoryJobManager>>();
        _jobManager = new InMemoryJobManager(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new InMemoryJobManager(null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Fact]
    public async Task CreateJobAsync_ValidInput_CreatesJob()
    {
        // Arrange
        var inputText = "Test input";
        var clientId = "test-client";

        // Act
        var job = await _jobManager.CreateJobAsync(inputText, clientId);

        // Assert
        job.Should().NotBeNull();
        job.Id.Should().NotBeEmpty();
        job.InputText.Should().Be(inputText);
        job.ClientId.Should().Be(clientId);
        job.Status.Should().Be(JobStatus.Pending);
        job.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        job.TotalCharacters.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateJobAsync_InvalidInput_ThrowsArgumentException(string? invalidInput)
    {
        // Act & Assert
        var act = async () => await _jobManager.CreateJobAsync(invalidInput!);
        await act.Should().ThrowAsync<ArgumentException>()
                 .WithParameterName("inputText");
    }

    [Fact]
    public async Task GetJobAsync_ReturnsStoredJobOrNull()
    {
        // Arrange
        var job = await _jobManager.CreateJobAsync("Test");

        // Act
        var retrievedJob = await _jobManager.GetJobAsync(job.Id);
        var missingJob = await _jobManager.GetJobAsync(Guid.NewGuid());

        // Assert
        retrievedJob.Should().NotBeNull();
        retrievedJob!.Id.Should().Be(job.Id);
        retrievedJob.InputText.Should().Be(job.InputText);
        missingJob.Should().BeNull();
    }

    [Fact]
    public async Task UpdateJobAsync_ValidJob_UpdatesJob()
    {
        // Arrange
        var job = await _jobManager.CreateJobAsync("Test");
        job.Status = JobStatus.Running;
        job.StartedAt = DateTime.UtcNow;

        // Act
        var updatedJob = await _jobManager.UpdateJobAsync(job);

        // Assert
        updatedJob.Should().NotBeNull();
        updatedJob.Status.Should().Be(JobStatus.Running);
        updatedJob.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateJobAsync_NullJob_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _jobManager.UpdateJobAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>()
                 .WithParameterName("job");
    }

    [Fact]
    public async Task CancelJobAsync_HonorsJobState()
    {
        // Arrange
        var cancellableJob = await _jobManager.CreateJobAsync("Pending job");
        var completedJob = await _jobManager.CreateJobAsync("Completed job");
        completedJob.Status = JobStatus.Completed;
        await _jobManager.UpdateJobAsync(completedJob);

        // Act
        var cancelled = await _jobManager.CancelJobAsync(cancellableJob.Id);
        var cancelledAfterCompletion = await _jobManager.CancelJobAsync(completedJob.Id);
        var cancelledUnknownJob = await _jobManager.CancelJobAsync(Guid.NewGuid());

        // Assert
        cancelled.Should().BeTrue();
        cancelledAfterCompletion.Should().BeFalse();
        cancelledUnknownJob.Should().BeFalse();

        var jobAfterCancel = await _jobManager.GetJobAsync(cancellableJob.Id);
        jobAfterCancel!.Status.Should().Be(JobStatus.Cancelled);
        jobAfterCancel.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetJobsByClientAsync_ExistingJobs_ReturnsClientJobs()
    {
        // Arrange
        var clientId = "test-client";
        var job1 = await _jobManager.CreateJobAsync("Test 1", clientId);
        var job2 = await _jobManager.CreateJobAsync("Test 2", clientId);
        var job3 = await _jobManager.CreateJobAsync("Test 3", "other-client");

        // Act
        var clientJobs = await _jobManager.GetJobsByClientAsync(clientId);

        // Assert
        clientJobs.Should().HaveCount(2);
        clientJobs.Should().Contain(j => j.Id == job1.Id);
        clientJobs.Should().Contain(j => j.Id == job2.Id);
        clientJobs.Should().NotContain(j => j.Id == job3.Id);
    }

    [Fact]
    public async Task CleanupOldJobsAsync_OldCompletedJobs_RemovesJobs()
    {
        // Arrange
        var oldJob = await _jobManager.CreateJobAsync("Old test");
        oldJob.Status = JobStatus.Completed;
        oldJob.CreatedAt = DateTime.UtcNow.AddDays(-2);
        await _jobManager.UpdateJobAsync(oldJob);

        var recentJob = await _jobManager.CreateJobAsync("Recent test");
        recentJob.Status = JobStatus.Completed;
        await _jobManager.UpdateJobAsync(recentJob);

        var olderThan = TimeSpan.FromDays(1);

        // Act
        var removedCount = await _jobManager.CleanupOldJobsAsync(olderThan);

        // Assert
        removedCount.Should().Be(1);
        
        var oldJobResult = await _jobManager.GetJobAsync(oldJob.Id);
        oldJobResult.Should().BeNull();
        
        var recentJobResult = await _jobManager.GetJobAsync(recentJob.Id);
        recentJobResult.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCancellationToken_ExistingJob_ReturnsToken()
    {
        // Arrange
        var job = await _jobManager.CreateJobAsync("Test");

        // Act
        var token = _jobManager.GetCancellationToken(job.Id);

        // Assert
        token.Should().NotBeNull();
        token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task GetCancellationToken_CancelledJob_ReturnsRequestedToken()
    {
        // Arrange
        var job = await _jobManager.CreateJobAsync("Test");
        await _jobManager.CancelJobAsync(job.Id);

        // Act
        var token = _jobManager.GetCancellationToken(job.Id);

        // Assert
        token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void GetCancellationToken_NonExistentJob_ReturnsNoneToken()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var token = _jobManager.GetCancellationToken(nonExistentId);

        // Assert
        token.Should().Be(CancellationToken.None);
    }
}