using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TextProcessor.Core.Models;
using TextProcessor.Core.Services;

namespace TextProcessor.Core.Tests.Services;

public class TextProcessingServiceTests
{
    private readonly Mock<ILogger<TextProcessingService>> _mockLogger;
    private readonly TextProcessingService _service;

    public TextProcessingServiceTests()
    {
        _mockLogger = new Mock<ILogger<TextProcessingService>>();
        _service = new TextProcessingService(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TextProcessingService(null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Fact]
    public void AnalyzeCharacterFrequency_BuildsOrderedCounts()
    {
        // Act
        var result = _service.AnalyzeCharacterFrequency("Hello, World!");
        var formatted = string.Join("", result.Select(cc => cc.ToString()));

        // Assert
        formatted.Should().Be(" 1!1,1H1W1d1e1l3o2r1");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AnalyzeCharacterFrequency_BlankInput_ReturnsEmpty(string? input)
    {
        // Act
        var result = _service.AnalyzeCharacterFrequency(input!);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Test", "VGVzdA==")]
    [InlineData("", "")]
    public void EncodeToBase64_HandlesCommonCases(string input, string expected)
    {
        // Act
        var result = _service.EncodeToBase64(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ProcessTextAsync_ValidInput_CompletesSuccessfully()
    {
        // Arrange
        var input = "Hi";
        var processedCharacters = new List<char>();
        // Act
        var result = await _service.ProcessTextAsync(
            input,
            args => processedCharacters.Add(args.Character),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FormattedResult.Should().Contain("H1i1/SGk=");
        processedCharacters.Should().NotBeEmpty();
        processedCharacters.Should().HaveCountGreaterThan(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ProcessTextAsync_BlankInput_ThrowsArgumentException(string? input)
    {
        // Act
        var act = async () => await _service.ProcessTextAsync(
            input!,
            _ => { },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
                 .WithParameterName("input");
    }

    [Fact]
    public async Task ProcessTextAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var input = "Test";
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var act = async () => await _service.ProcessTextAsync(
            input,
            _ => { },
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ProcessingResult_BuildFormattedResult_CreatesCorrectFormat()
    {
        // Arrange
        var result = new ProcessingResult
        {
            CharacterCounts = [new CharacterCount('A', 1), new CharacterCount('B', 2)],
            Base64Encoded = "QUJC"
        };

        // Act
        result.BuildFormattedResult();

        // Assert
        result.FormattedResult.Should().Be("A1B2/QUJC");
    }
}