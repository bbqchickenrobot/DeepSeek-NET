using System.Text.Json.Serialization;

namespace DeepSeek.Classes;

/// <summary>
/// Usage information for the request.
/// </summary>
public class Usage
{
    [JsonPropertyName("completion_tokens")]
    public long CompletionTokens { get; set; }

    [JsonPropertyName("prompt_tokens")]
    public long PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public long TotalTokens { get; set; }

    [JsonPropertyName("prompt_cache_hit_tokens")]
    public long PromptCacheHitTokens { get; set; }

    [JsonPropertyName("prompt_cache_miss_tokens")]
    public long PromptCacheMissTokens { get; set; }
}
