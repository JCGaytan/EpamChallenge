using System.Text;
using Microsoft.Extensions.Logging;
using TextProcessor.Core.Interfaces;
using TextProcessor.Core.Models;

namespace TextProcessor.Core.Services;

/// <summary>
/// Production-ready text processing service with proper error handling and cancellation support
/// </summary>
public class TextProcessingService : ITextProcessingService
{
    private readonly ILogger<TextProcessingService> _logger;
    private readonly Random _random;

    public TextProcessingService(ILogger<TextProcessingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    public async Task<ProcessingResult> ProcessTextAsync(
        string input, 
        Action<CharacterProcessedEventArgs> onCharacterProcessed,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input))
        {
            _logger.LogWarning("Attempted to process null or empty input text");
            throw new ArgumentException("Input text cannot be null or empty", nameof(input));
        }

        _logger.LogInformation("Starting text processing for input of length {Length}", input.Length);

        try
        {
            var result = new ProcessingResult();
            
            // Analyze character frequency
            result.CharacterCounts = AnalyzeCharacterFrequency(input);
            
            // Encode to Base64
            result.Base64Encoded = EncodeToBase64(input);
            
            // Build the formatted result
            result.BuildFormattedResult();
            
            // Simulate character-by-character processing with random delays
            await ProcessCharactersWithDelayAsync(result.FormattedResult, onCharacterProcessed, cancellationToken);
            
            _logger.LogInformation("Successfully completed text processing");
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Text processing was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during text processing");
            throw;
        }
    }

    public List<CharacterCount> AnalyzeCharacterFrequency(string input)
    {
        if (string.IsNullOrEmpty(input))
            return [];

        var characterFrequency = input
            .GroupBy(c => c)
            .Select(g => new CharacterCount(g.Key, g.Count()))
            .OrderBy(cc => cc.Character)
            .ToList();

        _logger.LogDebug("Analyzed {Count} unique characters in input", characterFrequency.Count);
        return characterFrequency;
    }

    public string EncodeToBase64(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var base64 = Convert.ToBase64String(bytes);
            _logger.LogDebug("Successfully encoded text to Base64, length: {Length}", base64.Length);
            return base64;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encode text to Base64");
            throw new InvalidOperationException("Failed to encode text to Base64", ex);
        }
    }

    /// <summary>
    /// Processes characters one by one with random delays to simulate heavy processing
    /// </summary>
    private async Task ProcessCharactersWithDelayAsync(
        string text, 
        Action<CharacterProcessedEventArgs> onCharacterProcessed,
        CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid(); // In real scenario, this would be passed from the job
        
        for (int i = 0; i < text.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Random delay between 1-5 seconds as specified in requirements
            var delayMs = _random.Next(1000, 5001);
            
            _logger.LogDebug("Processing character '{Character}' at position {Position} with delay {Delay}ms", 
                text[i], i, delayMs);
            
            await Task.Delay(delayMs, cancellationToken);
            
            // Notify about character processing
            var eventArgs = new CharacterProcessedEventArgs(text[i], i, text.Length, jobId);
            onCharacterProcessed?.Invoke(eventArgs);
        }
    }
}