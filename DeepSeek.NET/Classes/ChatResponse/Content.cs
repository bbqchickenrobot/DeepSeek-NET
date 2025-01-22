using System.Text.Json.Serialization;

namespace DeepSeek.Classes;

/// <summary>
/// Log probability information for a specific token.
/// </summary>
public class Content
{
    public string? Token { get; set; }

    public long Logprob { get; set; }

    public byte[] Bytes { get; set; } = [];

    [JsonPropertyName("top_logprobs")]
    public List<TopLogprobs> TopLogprobs { get; set; } = [];
}
