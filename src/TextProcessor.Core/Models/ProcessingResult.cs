namespace TextProcessor.Core.Models;

/// <summary>
/// Represents a character and its count in the processed text
/// </summary>
public record CharacterCount(char Character, int Count)
{
    public override string ToString() => $"{Character}{Count}";
}

/// <summary>
/// Result of text processing containing both character analysis and Base64 encoding
/// </summary>
public class ProcessingResult
{
    public List<CharacterCount> CharacterCounts { get; set; } = [];
    public string Base64Encoded { get; set; } = string.Empty;
    public string FormattedResult { get; set; } = string.Empty;
    
    /// <summary>
    /// Creates the formatted result string: "characters/base64"
    /// </summary>
    public void BuildFormattedResult()
    {
        var charactersSection = string.Join("", CharacterCounts.Select(cc => cc.ToString()));
        FormattedResult = $"{charactersSection}/{Base64Encoded}";
    }
}

/// <summary>
/// Event arguments for character processing events
/// </summary>
public class CharacterProcessedEventArgs : EventArgs
{
    public char Character { get; set; }
    public int Position { get; set; }
    public int Total { get; set; }
    public Guid JobId { get; set; }
    
    public CharacterProcessedEventArgs(char character, int position, int total, Guid jobId)
    {
        Character = character;
        Position = position;
        Total = total;
        JobId = jobId;
    }
}