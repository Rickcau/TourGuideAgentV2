using System;
using System.Text.Json.Serialization;

namespace TourGuideAgentV2.Models;


public class JobResults
{
    public string? JobId { get; set; }
    public List<JobResult>? Results { get; set; }
}

public class JobResult
{
    [JsonPropertyName("jobId")]
    public string? JobId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}



