namespace DeepSeek;

public static class Constants
{
    
    /// <summary>
    /// Base domain for API requests
    /// </summary>
    public const string BaseAddress = "https://api.deepseek.com";

    /// <summary>
    /// Chat completions endpoint
    /// </summary>
    public const string CompletionEndpoint = "/chat/completions";

    /// <summary>
    /// Models list endpoint
    /// </summary>
    public const string ModelsEndpoint = "/models";

    /// <summary>
    /// Stream completion indicator
    /// </summary>
    public const string StreamDoneSign = "[DONE]";
}


public static class Models
{
    public const string ModelChat = "deepseek-chat";

    public const string ModelReasoner = "deepseek-reasoner";
}
