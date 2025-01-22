using System.Text.Json.Serialization;

namespace DeepSeek.Classes;

public class Model
{
    public string? Id { get; set; }
    public string? Object { get; set; }


    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; set; }
}

