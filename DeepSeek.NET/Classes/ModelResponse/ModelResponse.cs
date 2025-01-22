namespace DeepSeek.Classes;

public class ModelResponse
{
    public string? Object { get; set; }
    public List<Model> Data { get; set; } = [];
}