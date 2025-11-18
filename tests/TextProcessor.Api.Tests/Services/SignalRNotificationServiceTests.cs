using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using TextProcessor.Api.Hubs;
using TextProcessor.Api.Services;
using TextProcessor.Core.Models;

namespace TextProcessor.Api.Tests.Services;

public class SignalRNotificationServiceTests
{
    private readonly Mock<IHubContext<ProcessingHub>> _mockHubContext;
    private readonly Mock<ILogger<SignalRNotificationService>> _mockLogger;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<IHubClients> _mockClients;
    private readonly SignalRNotificationService _service;

    public SignalRNotificationServiceTests()
    {
        _mockHubContext = new Mock<IHubContext<ProcessingHub>>();
        _mockLogger = new Mock<ILogger<SignalRNotificationService>>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockClients = new Mock<IHubClients>();
        
        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
        
        _service = new SignalRNotificationService(_mockHubContext.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullHubContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SignalRNotificationService(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("hubContext");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SignalRNotificationService(_mockHubContext.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task NotifyCharacterProcessedAsync_ValidParameters_CallsSignalRMethod()
    {
        // Arrange
        var clientId = "test-client";
        var jobId = Guid.NewGuid();
        var character = 'A';
        var progress = 50.25;

        // Act
        await _service.NotifyCharacterProcessedAsync(clientId, jobId, character, progress);

        // Assert
        _mockClients.Verify(c => c.Group($"Job_{jobId}"), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "CharacterProcessed",
                It.Is<object[]>(args => 
                    args.Length == 1 &&
                    args[0].ToString()!.Contains(jobId.ToString()) &&
                    args[0].ToString()!.Contains("A") &&
                    args[0].ToString()!.Contains("50.25")),
                default),
            Times.Once);
    }

    [Fact]
    public async Task NotifyJobCompletedAsync_ValidJob_CallsSignalRMethod()
    {
        // Arrange
        var clientId = "test-client";
        var job = new ProcessingJob
        {
            Id = Guid.NewGuid(),
            ProcessedText = "Test Result",
            CompletedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        // Act
        await _service.NotifyJobCompletedAsync(clientId, job);

        // Assert
        _mockClients.Verify(c => c.Group($"Job_{job.Id}"), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "JobCompleted",
                It.Is<object[]>(args => 
                    args.Length == 1 &&
                    args[0].ToString()!.Contains(job.Id.ToString()) &&
                    args[0].ToString()!.Contains("Test Result")),
                default),
            Times.Once);
    }

    [Fact]
    public async Task NotifyJobCancelledAsync_ValidParameters_CallsSignalRMethod()
    {
        // Arrange
        var clientId = "test-client";
        var jobId = Guid.NewGuid();

        // Act
        await _service.NotifyJobCancelledAsync(clientId, jobId);

        // Assert
        _mockClients.Verify(c => c.Group($"Job_{jobId}"), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "JobCancelled",
                It.Is<object[]>(args => 
                    args.Length == 1 &&
                    args[0].ToString()!.Contains(jobId.ToString())),
                default),
            Times.Once);
    }

    [Fact]
    public async Task NotifyJobFailedAsync_ValidParameters_CallsSignalRMethod()
    {
        // Arrange
        var clientId = "test-client";
        var jobId = Guid.NewGuid();
        var errorMessage = "Test error message";

        // Act
        await _service.NotifyJobFailedAsync(clientId, jobId, errorMessage);

        // Assert
        _mockClients.Verify(c => c.Group($"Job_{jobId}"), Times.Once);
        _mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "JobFailed",
                It.Is<object[]>(args => 
                    args.Length == 1 &&
                    args[0].ToString()!.Contains(jobId.ToString()) &&
                    args[0].ToString()!.Contains(errorMessage)),
                default),
            Times.Once);
    }

    [Fact]
    public async Task NotifyCharacterProcessedAsync_ExceptionInSignalR_DoesNotThrow()
    {
        // Arrange
        var clientId = "test-client";
        var jobId = Guid.NewGuid();
        var character = 'A';
        var progress = 50.0;

        _mockClientProxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                       .ThrowsAsync(new Exception("SignalR error"));

        // Act & Assert
        var act = async () => await _service.NotifyCharacterProcessedAsync(clientId, jobId, character, progress);
        await act.Should().NotThrowAsync();
    }
}