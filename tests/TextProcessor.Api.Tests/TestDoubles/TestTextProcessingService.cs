using System.Linq;
using System.Text;
using TextProcessor.Core.Interfaces;
using TextProcessor.Core.Models;

namespace TextProcessor.Api.Tests.TestDoubles;

/// <summary>
/// Deterministic text processing service for integration tests.
/// Preserves production formatting while using short delays so the
/// background job can be cancelled quickly without slowing the suite.
/// </summary>
public sealed class TestTextProcessingService : ITextProcessingService
{
    private static readonly TimeSpan CharacterDelay = TimeSpan.FromMilliseconds(75);

    public async Task<ProcessingResult> ProcessTextAsync(
        string input,
        Action<CharacterProcessedEventArgs> onCharacterProcessed,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Input text cannot be null or empty", nameof(input));
        }

        var result = new ProcessingResult
        {
            CharacterCounts = AnalyzeCharacterFrequency(input),
            Base64Encoded = EncodeToBase64(input)
        };

        result.BuildFormattedResult();

        for (var position = 0; position < result.FormattedResult.Length; position++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CharacterDelay > TimeSpan.Zero)
            {
                await Task.Delay(CharacterDelay, cancellationToken);
            }

            var character = result.FormattedResult[position];
            onCharacterProcessed?.Invoke(new CharacterProcessedEventArgs(
                character,
                position,
                result.FormattedResult.Length,
                Guid.Empty));
        }

        return result;
    }

    public List<CharacterCount> AnalyzeCharacterFrequency(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return [];
        }

        return input
            .GroupBy(c => c)
            .Select(group => new CharacterCount(group.Key, group.Count()))
            .OrderBy(character => character.Character)
            .ToList();
    }

    public string EncodeToBase64(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes);
    }
}
