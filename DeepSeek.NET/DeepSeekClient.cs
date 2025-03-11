using DeepSeek.Classes;
using Microsoft.Extensions.AI;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Channels;

namespace DeepSeek;

public class DeepSeekClient : IChatClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;
    private readonly ChatClientMetadata _metadata = new("deepseek", new Uri(Constants.BaseAddress));

    public JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultBufferSize = 1024
    };

    public string? ErrorMessage { get; private set; }

    public DeepSeekClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ConfigureHttpClient(apiKey);
    }

    public DeepSeekClient(string apiKey) : this(new HttpClient(), apiKey)
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    private void ConfigureHttpClient(string apiKey)
    {
        _httpClient.BaseAddress = new Uri(Constants.BaseAddress);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
    }

    public void SetTimeout(int seconds)
    {
        if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        _httpClient.Timeout = TimeSpan.FromSeconds(seconds);
    }

    public async Task<ModelResponse?> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(Constants.ModelsEndpoint, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorResponse(response);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ModelResponse>(JsonSerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Creates a chat completion
    /// </summary>
    /// <param name="request">Chat request parameters</param>
    /// <returns>Chat response or null if failed</returns>
    public async Task<DeepSeek.Classes.ChatResponse?> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        using var content = JsonContent.Create(request, options: JsonSerializerOptions);

        using var response = await _httpClient.PostAsync(Constants.CompletionEndpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorResponse(response);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<DeepSeek.Classes.ChatResponse>(JsonSerializerOptions, cancellationToken);
    }

    /// <summary>
    /// Creates a streaming chat completion
    /// </summary>
    /// <returns>Async enumerable of choices</returns>
    public async Task<IAsyncEnumerable<Choice>?> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        request.Stream = true;
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, Constants.CompletionEndpoint)
        {
            Content = content,
        };
        var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            return ProcessStream(stream);
        }
        else
        {
            var res = await response.Content.ReadAsStringAsync();
            ErrorMessage = res;
            return null;
        }
    }

    private IAsyncEnumerable<Choice> ProcessStream(Stream stream)
    {
        var reader = new StreamReader(stream);

        var channel = Channel.CreateUnbounded<Choice>();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line is null)
                {
                    break;
                }

                line = line?.Replace("data:", "").Trim();

                if (line == Constants.StreamDoneSign) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var chatResponse = JsonSerializer.Deserialize<DeepSeek.Classes.ChatResponse>(line, JsonSerializerOptions);

                var choice = chatResponse?.Choices.FirstOrDefault();
                if (choice is null) continue;

                await channel.Writer.WriteAsync(choice);
            }
            channel.Writer.Complete();
        });

        return channel.Reader.ReadAllAsync();
    }

    private async Task HandleErrorResponse(HttpResponseMessage response)
    {
        ErrorMessage = $"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #region IChatClient
    async Task<Microsoft.Extensions.AI.ChatResponse> IChatClient.GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        ChatRequest request = CreateChatRequest(messages, options);

        DeepSeek.Classes.ChatResponse? response = await ChatAsync(request, cancellationToken);
        ThrowIfRequestFailed(response);

        return CreateMeaiChatResponse(response);
    }

    async IAsyncEnumerable<ChatResponseUpdate> IChatClient.GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IAsyncEnumerable<Choice>? choices = await ChatStreamAsync(CreateChatRequest(messages, options), cancellationToken);
        ThrowIfRequestFailed(choices);

        string responseId = Guid.NewGuid().ToString("N");
        await foreach (var choice in choices)
        {
            yield return CreateChatResponseUpdate(choice, responseId);
        }
    }

    object? IChatClient.GetService(Type serviceType, object? serviceKey) =>
        serviceKey is not null ? null :
        serviceType == typeof(ChatClientMetadata) ? _metadata :
        serviceType?.IsInstanceOfType(this) is true ? this : 
        null;

    private void ThrowIfRequestFailed([NotNull] object? response)
    {
        if (response is null)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(ErrorMessage) ?
                $"Failed to get response" :
                $"Failed to get response: {ErrorMessage}");
        }
    }

    private static Microsoft.Extensions.AI.ChatResponse CreateMeaiChatResponse(DeepSeek.Classes.ChatResponse response)
    {
        Microsoft.Extensions.AI.ChatResponse completion = new([])
        {
            ResponseId = response.Id,
            ModelId = response.Model,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(response.Created)
        };

        if (response.Choices is { Count: > 0 })
        {
            completion.FinishReason ??= CreateFinishReason(response.Choices[0]);
            completion.Messages.Add(CreateChatMessage(response.Choices[0]));
        }

        if (response.Usage is Usage usage)
        {
            completion.Usage = new()
            {
                InputTokenCount = (int)usage.PromptTokens,
                TotalTokenCount = (int)usage.TotalTokens,
                OutputTokenCount = (int)usage.CompletionTokens,
                AdditionalCounts = new()
                {
                    [nameof(usage.PromptCacheHitTokens)] = (int)usage.PromptCacheHitTokens,
                    [nameof(usage.PromptCacheMissTokens)] = (int)usage.PromptCacheMissTokens,
                },
            };
        }

        return completion;
    }

    private static ChatFinishReason? CreateFinishReason(Choice choice) => 
        choice.FinishReason switch
        {
            "stop" => ChatFinishReason.Stop,
            "length" => ChatFinishReason.Length,
            "content_filter" => ChatFinishReason.ContentFilter,
            "tool_calls" => ChatFinishReason.ToolCalls,
            _ => null,
        };

    private static ChatMessage CreateChatMessage(Choice choice)
    {
        Message? choiceMessage = choice.Delta ?? choice.Message;

        ChatMessage m = new(CreateChatRole(choiceMessage), choiceMessage?.Content)
        {
            RawRepresentation = choice,
        };

        if (choice.Logprobs is not null)
        {
            (m.AdditionalProperties ??= []).Add(nameof(choice.Logprobs), choice.Logprobs);
        }

        return m;
    }

    private static ChatResponseUpdate CreateChatResponseUpdate(Choice choice, string responseId)
    {
        Message? choiceMessage = choice.Delta ?? choice.Message;

        ChatResponseUpdate update = new(CreateChatRole(choiceMessage), choiceMessage?.Content)
        {
            FinishReason = CreateFinishReason(choice),
            RawRepresentation = choice,
            ResponseId = responseId,
        };

        if (choice.Logprobs is not null)
        {
            (update.AdditionalProperties ??= []).Add(nameof(choice.Logprobs), choice.Logprobs);
        }

        return update;
    }

    private static ChatRole CreateChatRole(Message? m) =>
        m?.Role switch
        {
            "user" => ChatRole.User,
            "system" => ChatRole.System,
            _ => ChatRole.Assistant,
        };

    private static ChatRequest CreateChatRequest(IEnumerable<ChatMessage> chatMessages, ChatOptions? options)
    {
        ChatRequest request = new();

        if (options is not null)
        {
            if (options.ModelId is not null) request.Model = options.ModelId;
            if (options.FrequencyPenalty is not null) request.FrequencyPenalty = options.FrequencyPenalty.Value;
            if (options.MaxOutputTokens is not null) request.MaxTokens = options.MaxOutputTokens.Value;
            if (options.PresencePenalty is not null) request.PresencePenalty = options.PresencePenalty.Value;
            if (options.StopSequences is not null) request.Stop = [.. options.StopSequences];
            if (options.Temperature is not null) request.Temperature = options.Temperature.Value;
            if (options.TopP is not null) request.TopP = options.TopP.Value;
            if (options.AdditionalProperties?.TryGetValue(nameof(request.Logprobs), out bool logprobs) is true) request.Logprobs = logprobs;
            if (options.AdditionalProperties?.TryGetValue(nameof(request.TopLogprobs), out int topLogprobs) is true) request.TopLogprobs = topLogprobs;
        }

        List<Message> messages = [];
        foreach (var message in chatMessages)
        {
            string role;
            if (message.Role == ChatRole.User) role = "user";
            else if (message.Role == ChatRole.Assistant) role = "assistant";
            else if (message.Role == ChatRole.System) role = "system";
            else continue;

            string text = string.Concat(message.Contents.OfType<TextContent>());

            if (!string.IsNullOrWhiteSpace(text))
            {
                messages.Add(new() { Content = text, Role = role });
            }
        }

        request.Messages = messages.ToArray();
        return request;
    }
    #endregion
}