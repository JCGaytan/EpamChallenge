using FluentAssertions;
using TextProcessor.Core.Models;

namespace TextProcessor.Core.Tests.Models;

public class ProcessingJobTests
{
    [Fact]
    public void ProcessingJob_DefaultConstructor_SetsDefaults()
    {
        // Act
        var job = new ProcessingJob();

        // Assert
        job.Id.Should().NotBeEmpty();
        job.InputText.Should().BeEmpty();
        job.ProcessedText.Should().BeNull();
        job.Status.Should().Be(JobStatus.Pending);
        job.ProcessedCharacters.Should().Be(0);
        job.TotalCharacters.Should().Be(0);
        job.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.ClientId.Should().BeNull();
        job.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(25, 100, 25)]
    [InlineData(100, 100, 100)]
    [InlineData(0, 100, 0)]
    public void ProgressPercentage_CalculatesCorrectly(int processed, int total, double expected)
    {
        // Arrange
        var job = new ProcessingJob
        {
            ProcessedCharacters = processed,
            TotalCharacters = total
        };

        // Act
        var result = job.ProgressPercentage;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ProgressPercentage_ZeroTotal_ReturnsZero()
    {
        // Arrange
        var job = new ProcessingJob
        {
            ProcessedCharacters = 50,
            TotalCharacters = 0
        };

        // Act
        var result = job.ProgressPercentage;

        // Assert
        result.Should().Be(0);
    }

    [Theory]
    [InlineData(JobStatus.Pending, true)]
    [InlineData(JobStatus.Running, true)]
    [InlineData(JobStatus.Completed, false)]
    [InlineData(JobStatus.Cancelled, false)]
    [InlineData(JobStatus.Failed, false)]
    public void CanBeCancelled_ReturnsCorrectValue(JobStatus status, bool expected)
    {
        // Arrange
        var job = new ProcessingJob { Status = status };

        // Act
        var result = job.CanBeCancelled;

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(JobStatus.Pending, false)]
    [InlineData(JobStatus.Running, false)]
    [InlineData(JobStatus.Completed, true)]
    [InlineData(JobStatus.Cancelled, true)]
    [InlineData(JobStatus.Failed, true)]
    public void IsFinished_ReturnsCorrectValue(JobStatus status, bool expected)
    {
        // Arrange
        var job = new ProcessingJob { Status = status };

        // Act
        var result = job.IsFinished;

        // Assert
        result.Should().Be(expected);
    }
}

public class ProcessingResultTests
{
    [Fact]
    public void ProcessingResult_DefaultConstructor_SetsDefaults()
    {
        // Act
        var result = new ProcessingResult();

        // Assert
        result.CharacterCounts.Should().NotBeNull().And.BeEmpty();
        result.Base64Encoded.Should().BeEmpty();
        result.FormattedResult.Should().BeEmpty();
    }

    [Fact]
    public void BuildFormattedResult_WithData_CreatesCorrectFormat()
    {
        // Arrange
        var result = new ProcessingResult
        {
            CharacterCounts = 
            [
                new CharacterCount('A', 2),
                new CharacterCount('B', 1),
                new CharacterCount(' ', 1)
            ],
            Base64Encoded = "QSBBQg=="
        };

        // Act
        result.BuildFormattedResult();

        // Assert
        result.FormattedResult.Should().Be("A2B1 1/QSBBQg==");
    }

    [Fact]
    public void BuildFormattedResult_EmptyData_CreatesSlashOnly()
    {
        // Arrange
        var result = new ProcessingResult
        {
            CharacterCounts = [],
            Base64Encoded = ""
        };

        // Act
        result.BuildFormattedResult();

        // Assert
        result.FormattedResult.Should().Be("/");
    }
}

public class CharacterCountTests
{
    [Fact]
    public void CharacterCount_Constructor_SetsProperties()
    {
        // Arrange
        var character = 'A';
        var count = 5;

        // Act
        var charCount = new CharacterCount(character, count);

        // Assert
        charCount.Character.Should().Be(character);
        charCount.Count.Should().Be(count);
    }

    [Theory]
    [InlineData('A', 1, "A1")]
    [InlineData('Z', 10, "Z10")]
    [InlineData(' ', 5, " 5")]
    [InlineData('!', 0, "!0")]
    public void ToString_ReturnsCorrectFormat(char character, int count, string expected)
    {
        // Arrange
        var charCount = new CharacterCount(character, count);

        // Act
        var result = charCount.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CharacterCount_Equality_WorksCorrectly()
    {
        // Arrange
        var charCount1 = new CharacterCount('A', 5);
        var charCount2 = new CharacterCount('A', 5);
        var charCount3 = new CharacterCount('B', 5);
        var charCount4 = new CharacterCount('A', 3);

        // Act & Assert
        charCount1.Should().Be(charCount2);
        charCount1.Should().NotBe(charCount3);
        charCount1.Should().NotBe(charCount4);
    }
}

public class CharacterProcessedEventArgsTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var character = 'X';
        var position = 42;
        var total = 100;
        var jobId = Guid.NewGuid();

        // Act
        var eventArgs = new CharacterProcessedEventArgs(character, position, total, jobId);

        // Assert
        eventArgs.Character.Should().Be(character);
        eventArgs.Position.Should().Be(position);
        eventArgs.Total.Should().Be(total);
        eventArgs.JobId.Should().Be(jobId);
    }
}