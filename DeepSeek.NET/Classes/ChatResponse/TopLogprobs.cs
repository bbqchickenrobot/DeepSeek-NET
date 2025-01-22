namespace DeepSeek.Classes;

/// <summary>
/// Top log probability information for a specific token.
/// </summary>
public class TopLogprobs
{
    public string? Token { get; set; }

    public long Logprob { get; set; }

    public byte[] Bytes { get; set; } = [];
}
