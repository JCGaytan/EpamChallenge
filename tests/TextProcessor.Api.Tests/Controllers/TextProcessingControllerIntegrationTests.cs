using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TextProcessor.Api.Controllers;
using TextProcessor.Core.Interfaces;

namespace TextProcessor.Api.Tests.Controllers;

public class TextProcessingControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TextProcessingControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task ProcessText_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new ProcessTextRequest { Text = "Hello, World!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/textprocessing/process", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var job = JsonSerializer.Deserialize<ProcessingJobDto>(responseContent, JsonOptions);

        job.Should().NotBeNull();
        job!.Id.Should().NotBeEmpty();
        job.InputText.Should().Be("Hello, World!");
        job.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ProcessText_EmptyText_ReturnsBadRequest()
    {
        // Arrange
        var request = new ProcessTextRequest { Text = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/textprocessing/process", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProcessText_NullText_ReturnsBadRequest()
    {
        // Arrange
        var request = new ProcessTextRequest { Text = null! };

        // Act
        var response = await _client.PostAsJsonAsync("/api/textprocessing/process", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetJob_ExistingJob_ReturnsOk()
    {
        // Arrange - First create a job
        var createRequest = new ProcessTextRequest { Text = "Test" };
        var createResponse = await _client.PostAsJsonAsync("/api/textprocessing/process", createRequest);
        createResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        
        var createResponseContent = await createResponse.Content.ReadAsStringAsync();
        var createdJob = JsonSerializer.Deserialize<ProcessingJobDto>(createResponseContent, JsonOptions);

        // Act
        var response = await _client.GetAsync($"/api/textprocessing/jobs/{createdJob!.Id}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var job = JsonSerializer.Deserialize<ProcessingJobDto>(responseContent, JsonOptions);

        job.Should().NotBeNull();
        job!.Id.Should().Be(createdJob.Id);
        job.InputText.Should().Be("Test");
    }

    [Fact]
    public async Task GetJob_NonExistentJob_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/textprocessing/jobs/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetJobs_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/textprocessing/jobs");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var jobs = JsonSerializer.Deserialize<ProcessingJobDto[]>(responseContent, JsonOptions);

        jobs.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelJob_RunningJob_ReturnsOkAndMarksJobCancelled()
    {
        // Arrange
        var processRequest = new HttpRequestMessage(HttpMethod.Post, "/api/textprocessing/process")
        {
            Content = JsonContent.Create(new ProcessTextRequest { Text = "Cancel me" })
        };
        processRequest.Headers.Add("X-SignalR-ConnectionId", Guid.NewGuid().ToString());

        var createResponse = await _client.SendAsync(processRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdJob = await DeserializeAsync<ProcessingJobDto>(createResponse);
        createdJob.Should().NotBeNull();

        // Act - wait until processing begins so the cancellation token is registered
        await WaitForJobStatusAsync(createdJob!.Id, "Running", TimeSpan.FromSeconds(5));

        var cancelResponse = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/textprocessing/jobs/{createdJob.Id}/cancel"));

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelledJob = await WaitForJobStatusAsync(createdJob.Id, "Cancelled", TimeSpan.FromSeconds(5));
        cancelledJob.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelJob_CompletedJob_ReturnsBadRequest()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/textprocessing/process", new ProcessTextRequest { Text = "Already done" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdJob = await DeserializeAsync<ProcessingJobDto>(createResponse);
        createdJob.Should().NotBeNull();

        // Wait for completion
        await WaitForJobStatusAsync(createdJob!.Id, "Completed", TimeSpan.FromSeconds(10));

        // Act
        var cancelResponse = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/textprocessing/jobs/{createdJob.Id}/cancel"));

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await cancelResponse.Content.ReadAsStringAsync();
        responseContent.Should().Contain("cannot be cancelled");
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ping_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/ping");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("OK");
    }

    private async Task<ProcessingJobDto> WaitForJobStatusAsync(Guid jobId, string expectedStatus, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            var job = await GetJobAsync(jobId);
            if (job is not null && string.Equals(job.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))
            {
                return job;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Timed out waiting for job {jobId} to reach status '{expectedStatus}'.");
    }

    private async Task<ProcessingJobDto?> GetJobAsync(Guid jobId)
    {
        var response = await _client.GetAsync($"/api/textprocessing/jobs/{jobId}");

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        return await DeserializeAsync<ProcessingJobDto>(response);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseContent, JsonOptions);
    }
}