# DeepSeek.NET

![NuGet Version](https://img.shields.io/nuget/v/DeepSeek.NET)  
A modern .NET wrapper for the DeepSeek AI API – integrate conversational AI into your applications with ease.

- **Model Discovery**: Retrieve available AI models effortlessly.
- **Smart Conversations**: Execute both single-turn and streaming multi-turn dialogues.
- **Streaming Support**: Real-time response handling for dynamic interactions.
- **Modern .NET Integration**: Built with .NET 8 and `HttpClient` best practices.

## 📋 Prerequisites

- **DeepSeek API Key**: Obtain from [DeepSeek Platform](https://platform.deepseek.com/)
- **.NET 8+**: Target framework requirement

## 📦 Installation

```bash
dotnet add package DeepSeek.NET
```

## 🛠️ Quick Start

### Initialize Client

```csharp
// Simple initialization
var client = new DeepSeekClient("your_api_key_here");

// Or with custom HttpClient
var httpClient = new HttpClient();
var factoryClient = new DeepSeekClient(httpClient, "your_api_key_here");
```

(You can change the HttpClient Timeout with `client.setTimeout(int seconds)`)

### Discover Available Models

DeepSeek offers two AI models for different use cases:

- **`Models.ModelChat`**: Standard conversational AI model, ideal for chat interactions.
- **`Models.ModelReasoner`**: Includes an additional reasoning phase, useful for deeper analytical responses.

```csharp
var models = await client.ListModelsAsync();
if (models?.Data is null)
{
    Console.WriteLine($"Error: {client.ErrorMsg}");
    return;
}

foreach (var model in models.Data)
{
    Console.WriteLine($"- {model.Id}: {model.Capabilities}");
}
```

### Basic Chat Interaction

When constructing a conversation, you can use different types of messages to define roles:

- **`Message.NewSystemMessage(content)`**: Used for system instructions or context setting.
- **`Message.NewAssistantMessage(content)`**: Represents a response from the AI assistant.
- **`Message.NewUserMessage(content)`**: Represents a message from the user.

```csharp
var chatRequest = new ChatRequest
{
    Messages = [
        Message.NewUserMessage("Explain quantum computing in 3 sentences"),
        Message.NewAssistantMessage("..."),
        Message.NewUserMessage("Now simplify it for a 5th grader")
    ],
    Model = Models.ModelChat
};

var response = await client.ChatAsync(chatRequest);
Console.WriteLine(response?.Choices.First().Message.Content ?? "No response");
```

### Real-Time Streaming

```csharp
var streamRequest = new ChatRequest
{
    Messages = [
        Message.NewUserMessage("Tell a 100-word sci-fi story") }
    ],
    Model = Models.ModelChat
};

var stream = await client.ChatStreamAsync(streamRequest);
if (stream is null) return;

await foreach (var chunk in stream)
{
    Console.Write(chunk.Delta?.Content);
}
```

### Read Reasoning Content

Only in case of `Models.Reasoner`, you will have available also the Reasoning Content

```csharp
var chatRequest = new ChatRequest
{
    Messages = [
        Message.NewUserMessage("Hello! Tell me an interesting fact about space.")
    ],
    Model = Models.ModelReasoner
};

var response = await client.ChatAsync(chatRequest);
var reasoningContent = response?.Choices.First().Message.ReasoningContent ?? String.Empty;
Console.WriteLine(reasoningContent);
```

## 🤝 Contributing

We welcome contributions! Please follow our contribution guidelines when:
- Reporting issues
- Suggesting enhancements
- Submitting pull requests

## License

MIT License – See [LICENSE](LICENSE) for details.
