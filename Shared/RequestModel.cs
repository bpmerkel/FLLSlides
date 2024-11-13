using System.Text.Json.Serialization;

namespace FLLSlides.Shared;

/// <summary>
/// Represents a request model containing event, judging, robot game configurations, and teams.
/// </summary>
public class RequestModel
{
    /// <summary>
    /// Gets or sets the name of the request.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the array of teams.
    /// </summary>
    [JsonPropertyName("teams")]
    public Team[] Teams { get; set; } = [];
}
