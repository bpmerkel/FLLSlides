using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using FLLSlides.Shared;
using System;
using Grpc.Core;
using System.Linq;

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
            .Select(f => new TemplateDetails
            {
                Name = Path.GetFileName(f),
                Fields = ["Field1", "Field2"]
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
        using var writer = new StreamWriter(outstream);
        writer.WriteLine("testing");
    }
}