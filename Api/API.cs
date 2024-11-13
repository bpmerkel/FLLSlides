using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using FLLSlides.Shared;
using System;

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
        using var ms = ProcessRequest(request);
        ms.Position = 0;
        await ms.CopyToAsync(response.Body);
        logger.LogMetric("TransactionTimeMS", sw.Elapsed.TotalMilliseconds);
        return response;
    }

    private static MemoryStream ProcessRequest(RequestModel request)
    {
        var ms = new MemoryStream();
        using var writer = new StreamWriter(ms);
        writer.WriteLine("testing");
        return ms;
    }
}