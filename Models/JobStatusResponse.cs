using System;
using System.Text.Json.Serialization;

namespace TourGuideAgentV2.Models;

public class JobStatus
{
    [JsonPropertyName("jobId")]
    public string? JobId { get; set; }

    [JsonPropertyName("type")]
    public string? type { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
