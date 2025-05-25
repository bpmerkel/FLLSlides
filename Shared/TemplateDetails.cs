namespace FLLSlides.Shared;

/// <summary>
/// Defines the details of a template used in the application.
/// </summary>
public class TemplateDetails
{
    public string Name { get; set; }
    public string Filename { get; set; }
    public string[] Fields { get; set; } = [];
}