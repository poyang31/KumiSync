using System.Text.Json.Serialization;

namespace KimiSync.Models;

public sealed class SyncData
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    [JsonPropertyName("__v")]
    public int? Version { get; set; }
}
