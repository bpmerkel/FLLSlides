using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using FLLSlides.Shared;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using ShapeCrawler;

namespace ApiIsolated;

/// <summary>
/// Represents a class that handles HTTP triggers.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpTrigger"/> class.
/// </remarks>
public partial class API
{
    /// <summary>
    /// Runs the HTTP trigger.
    /// </summary>
    /// <param name="req">The HTTP request data.</param>
    /// <param name="executionContext">The context in which the function is executed.</param>
    /// <returns>The HTTP response data.</returns>
    [Function(nameof(GetTemplateDetails))]
    public static async Task<HttpResponseData> GetTemplateDetails([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, FunctionContext executionContext)
    {
        var sw = Stopwatch.StartNew();

        var logger = executionContext.GetLogger("HttpTrigger1");
        logger.LogInformation("GetTemplateDetails function processed a request.");

        var request = await req.ReadFromJsonAsync<TemplateRequest>();

        // validate the incoming request
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var response = req.CreateResponse(HttpStatusCode.OK);

        // generate the response
        var tr = new TemplateResponse
        {
            Request = request
        };

        // get files in the templates folder
        // open each and find all the fields
        // return the fields
        var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
        var files = Directory.GetFiles(folder, "*.pptx");
        tr.Templates = files
            .Select(f =>
            {
                var pres = new Presentation(f);
                var fields = pres.Slides
                    .SelectMany(slide => slide.TextFrames()
                        .Where(textbox => textbox.Text.Contains("{"))
                        .SelectMany(textbox => Regex.Matches(textbox.Text, @"\{(.*)\}", RegexOptions.Multiline)
                            .Cast<Match>()
                            .Select(m => m.Groups[1].Value)
                            .ToArray()
                        )
                    )
                    .ToArray();
                return new TemplateDetails
                {
                    Name = Path.GetFileNameWithoutExtension(f),
                    Filename = Path.GetFileName(f),
                    Fields = fields
                };
            })
            .ToArray();

        await response.WriteAsJsonAsync(tr);
        logger.LogMetric("TransactionTimeMS", sw.Elapsed.TotalMilliseconds);
        return response;
    }

    /// <summary>
    /// Runs the HTTP trigger.
    /// </summary>
    /// <param name="req">The HTTP request data.</param>
    /// <param name="executionContext">The context in which the function is executed.</param>
    /// <returns>The HTTP response data.</returns>
    [Function(nameof(GenerateDeck))]
    public static async Task<HttpResponseData> GenerateDeck([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, FunctionContext executionContext)
    {
        var sw = Stopwatch.StartNew();

        var logger = executionContext.GetLogger("HttpTrigger1");
        logger.LogInformation("GenerateDeck function processed a request.");

        var request = await req.ReadFromJsonAsync<RequestModel>();

        // validate the incoming request
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var response = req.CreateResponse(HttpStatusCode.OK);
        ProcessRequest(request, response.Body);
        logger.LogMetric("TransactionTimeMS", sw.Elapsed.TotalMilliseconds);
        return response;
    }

    private static void ProcessRequest(RequestModel request, Stream outstream)
    {
        var template = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", request.TemplateDetails.Filename);
        var pres = new Presentation(template);
        var edits = pres.Slides
            .SelectMany(slide => slide.TextFrames()
                .Where(textbox => textbox.Text.Contains("{"))
                .Select(textbox => new
                {
                    textbox,
                    groups = Regex.Matches(textbox.Text, @"\{(.*)\}", RegexOptions.Multiline)
                        .Cast<Match>()
                        .Select(m => m.Groups.Cast<Group>().ToArray())
                        .ToArray()
                })
            )
            .ToArray();

        foreach (var edit in edits)
        {
            foreach (var paragraph in edit.textbox.Paragraphs)
            {
                foreach (var group in edit.groups)
                {
                    var find = group[0].Value;
                    var key = group[1].Value;
                    var replacement = request.Substitutions.TryGetValue(key, out string sub) ? sub : string.Empty;
                    paragraph.ReplaceText(find, replacement);
                }
            }
        }

        pres.SaveAs(outstream);
    }
}