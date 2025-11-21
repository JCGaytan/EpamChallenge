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

    [Fact]
    public async Task ProcessText_MultipleClients_CanRunConcurrently()
    {
        // Arrange - Create requests for different clients
        var client1Id = Guid.NewGuid().ToString();
        var client2Id = Guid.NewGuid().ToString();
        
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/textprocessing/process")
        {
            Content = JsonContent.Create(new ProcessTextRequest { Text = "Client 1 text" })
        };
        request1.Headers.Add("X-SignalR-ConnectionId", client1Id);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/textprocessing/process")
        {
            Content = JsonContent.Create(new ProcessTextRequest { Text = "Client 2 text" })
        };
        request2.Headers.Add("X-SignalR-ConnectionId", client2Id);

        // Act - Start both processes simultaneously
        var response1Task = _client.SendAsync(request1);
        var response2Task = _client.SendAsync(request2);

        var responses = await Task.WhenAll(response1Task, response2Task);

        // Assert - Both should succeed
        responses[0].StatusCode.Should().Be(HttpStatusCode.Created);
        responses[1].StatusCode.Should().Be(HttpStatusCode.Created);

        var job1 = await DeserializeAsync<ProcessingJobDto>(responses[0]);
        var job2 = await DeserializeAsync<ProcessingJobDto>(responses[1]);

        job1.Should().NotBeNull();
        job2.Should().NotBeNull();
        job1!.Id.Should().NotBe(job2!.Id); // Different job IDs
        job1.InputText.Should().Be("Client 1 text");
        job2.InputText.Should().Be("Client 2 text");
    }

    [Fact]
    public async Task GetJobs_DifferentClients_OnlyReturnsOwnJobs()
    {
        // Arrange - Create jobs for different clients
        var client1Id = Guid.NewGuid().ToString();
        var client2Id = Guid.NewGuid().ToString();
        
        // Create job for client 1
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/textprocessing/process")
        {
            Content = JsonContent.Create(new ProcessTextRequest { Text = "Client 1 job" })
        };
        request1.Headers.Add("X-SignalR-ConnectionId", client1Id);
        
        var createResponse1 = await _client.SendAsync(request1);
        createResponse1.StatusCode.Should().Be(HttpStatusCode.Created);
        var job1 = await DeserializeAsync<ProcessingJobDto>(createResponse1);

        // Create job for client 2
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/textprocessing/process")
        {
            Content = JsonContent.Create(new ProcessTextRequest { Text = "Client 2 job" })
        };
        request2.Headers.Add("X-SignalR-ConnectionId", client2Id);
        
        var createResponse2 = await _client.SendAsync(request2);
        createResponse2.StatusCode.Should().Be(HttpStatusCode.Created);
        var job2 = await DeserializeAsync<ProcessingJobDto>(createResponse2);

        // Act - Get jobs for each client
        var getJobsRequest1 = new HttpRequestMessage(HttpMethod.Get, "/api/textprocessing/jobs");
        getJobsRequest1.Headers.Add("X-SignalR-ConnectionId", client1Id);
        var jobsResponse1 = await _client.SendAsync(getJobsRequest1);
        
        var getJobsRequest2 = new HttpRequestMessage(HttpMethod.Get, "/api/textprocessing/jobs");
        getJobsRequest2.Headers.Add("X-SignalR-ConnectionId", client2Id);
        var jobsResponse2 = await _client.SendAsync(getJobsRequest2);

        // Assert
        jobsResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        jobsResponse2.StatusCode.Should().Be(HttpStatusCode.OK);

        var jobs1 = await DeserializeAsync<ProcessingJobDto[]>(jobsResponse1);
        var jobs2 = await DeserializeAsync<ProcessingJobDto[]>(jobsResponse2);

        jobs1.Should().NotBeNull();
        jobs2.Should().NotBeNull();

        // Each client should see their own job
        jobs1!.Should().Contain(j => j.Id == job1!.Id);
        jobs2!.Should().Contain(j => j.Id == job2!.Id);

        // Each client should NOT see the other's job
        jobs1.Should().NotContain(j => j.Id == job2!.Id);
        jobs2.Should().NotContain(j => j.Id == job1!.Id);
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