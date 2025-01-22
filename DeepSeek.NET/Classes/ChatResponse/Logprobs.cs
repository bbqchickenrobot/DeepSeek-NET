namespace DeepSeek.Classes;

/// <summary>
/// Log probability information.
/// </summary>
public class Logprobs
{
    /// <summary>
    /// A list containing log probability information for the output tokens.
    /// </summary>
    public List<Content> Content { get; set; } = [];
}
