using System.Text.Json.Serialization;

namespace FLLSlides.Shared;

public class TemplateRequest
{
    /// <summary>
    /// Gets or sets the name of the request.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }
}