using DeepSeek.Classes;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Channels;
using static System.Net.WebRequestMethods;

namespace DeepSeek;

public class DeepSeekClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

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
    public async Task<ChatResponse?> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        request.Stream = false;
        using var content = JsonContent.Create(request, options: JsonSerializerOptions);

        using var response = await _httpClient.PostAsync(Constants.CompletionEndpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorResponse(response);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ChatResponse>(JsonSerializerOptions, cancellationToken);
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
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                line = line?.Replace("data:", "").Trim();
                
                if (line == Constants.StreamDoneSign) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var chatResponse = JsonSerializer.Deserialize<ChatResponse>(line, JsonSerializerOptions);

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
}