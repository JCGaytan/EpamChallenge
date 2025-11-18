using TextProcessor.Core.Models;

namespace TextProcessor.Core.Interfaces;

/// <summary>
/// Interface for text processing services that handle character analysis and encoding
/// </summary>
public interface ITextProcessingService
{
    /// <summary>
    /// Processes text asynchronously with real-time character streaming
    /// </summary>
    /// <param name="input">The text to process</param>
    /// <param name="onCharacterProcessed">Callback invoked for each processed character</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The complete processing result</returns>
    Task<ProcessingResult> ProcessTextAsync(
        string input, 
        Action<CharacterProcessedEventArgs> onCharacterProcessed,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes character frequency in the input text
    /// </summary>
    /// <param name="input">The text to analyze</param>
    /// <returns>List of character counts sorted by character</returns>
    List<CharacterCount> AnalyzeCharacterFrequency(string input);

    /// <summary>
    /// Encodes text to Base64 format
    /// </summary>
    /// <param name="input">The text to encode</param>
    /// <returns>Base64 encoded string</returns>
    string EncodeToBase64(string input);
}