using Microsoft.AspNetCore.Mvc;
using TourGuideAgentV2.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using TourGuideAgentV2.Plugins;
using TourGuideAgentV2.Plugins.Orchestrator;
using TourGuideAgentV2.Plugins.TripPlanner;
using TourGuideAgentV2.Services;

namespace TourGuideAgentV2.Controllers;

[ApiController]
[Route("[controller]")]
public class ChatStreamController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chat;

    private readonly IChatHistoryManager _chatHistoryManager;
    private readonly JobResultsCacheService _jobResultsCacheService;
    private readonly ILogger<ChatStreamController> _logger;

    private readonly ILoggerFactory _loggerFactory;

    public ChatStreamController(ILogger<ChatStreamController> logger, Kernel kernel, IChatCompletionService chat, IChatHistoryManager chathistorymanager, JobResultsCacheService jobresultscacheservice,ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _kernel = kernel;
        _chat = chat;
        _chatHistoryManager = chathistorymanager;
        _jobResultsCacheService = jobresultscacheservice;
        _loggerFactory = loggerFactory;
    }

    [HttpPost("chat")]
    public async Task MainMessageLoop([FromBody] ChatRequest request)
    {
        // curl -N -X 'POST' 'https://localhost:7227/ChatStream/chat' -H 'accept: */*' -H 'Content-Type: application/json' -d '{"vehicleId": "string","prompt": "string"}'
        HttpContext.Response.Headers.Append("Transfer-Encoding", "chunked");
        HttpContext.Response.Headers.Append("Content-Type", "text/plain");
        await HttpContext.Response.StartAsync();

        _logger.LogInformation("GetStreamResponse: user's request : " + request);

        using var writer = new StreamWriter(HttpContext.Response.BodyWriter.AsStream());
            // using var writer = new StreamWriter(HttpContext.Response.Body, leaveOpen: true);
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Prompt) || string.IsNullOrEmpty(request.VehicleId) || string.IsNullOrEmpty(request.ClientId))
                {
                    await WriteChunk(writer, "BadRequest: Prompt, VehicleId or ClientId cannot be empty!");
                    return;
                }

                var _vehicleId = request.VehicleId;
                var _clientId = request.ClientId;

                var chatHistory = _chatHistoryManager.GetOrCreateChatHistory(_clientId);
                chatHistory.AddUserMessage($"ClientId: {_clientId}, VehicleId: {_vehicleId}");

                _kernel.ImportPluginFromObject(new TripPlannerPlugin(_chat, _chatHistoryManager, _jobResultsCacheService, _kernel), "TripPlannerPlugin");

                var orchestratorLogger = _loggerFactory.CreateLogger<OrchestratorPlugin>();
                OrchestratorPlugin masterOrchestrator = new OrchestratorPlugin(_kernel, _chatHistoryManager, orchestratorLogger);
                
                // Main mesage loop for the OrchestratorPlugin
                await foreach (var chunk in masterOrchestrator.MessageLoopStreamAsync(request.Prompt, _clientId, _vehicleId!))
                {
                    if (chunk == null)
                    {
                        await WriteChunk(writer, "ServerError: No response from agent");
                    }
                    await WriteChunk(writer, chunk!.ToString());
                    //await Task.Delay(200);
                }
                
                // Let's check to see if we have any function calls.
                // We have to detect the function calls that need to be executed and use this special approach to stream the response back to the client. 
                if (masterOrchestrator.FunctionsToCall)
                {
                    _logger.LogInformation($"Function calls to be executed:True");
                    var functionCalls = masterOrchestrator.GetFunctionsToCall();
                    if (functionCalls != null && functionCalls.Any())
                    {
                        var functionCall = functionCalls.First(); 
                        switch (functionCall.PluginName)
                        {
                            case "TripPlannerPlugin":  // you need to remove the Async from the function name
                                 if (functionCall.FunctionName == "SuggestPlaces")
                                 {
                                    // We have to use this approach as the FunctionCallContent.InvokeAsync() does not result a FunctionResult that can be streamed back to the client.
                                    // In this example, the Suggest places is streaming back the whole response to the client in JSON format as a chunked response.  You could avoid this by using the JobID approach if this works for you.
                                    // Otherwise you need to change the prompt in SuggestPlaces to only reponse with a small amount of data and then use a JobID to collect the rest of the data in the background. 
                                    // You will need to use the JobID to check the status of the data collection and retrieve the data when ready.
                                    // The JobID approach something can be consider for longer running tasks.  But, for this example we are streaming the whole response back to the client, which may not be the best approach for large data sets.
                                    KernelFunction suggestPlacesFunction = _kernel.Plugins.GetFunction("TripPlannerPlugin", "SuggestPlaces");
                                    KernelArguments suggestPlacesArgs = functionCall.Arguments!;
                                    KernelArguments args = new() {
                                        { "clientId", _clientId },
                                        { "location", "Ashville, NC" },
                                        { "categories", "Hiking and Biking" },
                                        { "travelCompanions", "alone" },
                                    };
                                    FunctionResult result = await _kernel.InvokeAsync(suggestPlacesFunction, suggestPlacesArgs);
                                    IAsyncEnumerable<string> datastream = result.GetValue<IAsyncEnumerable<string>>()!;
                                    await foreach (var content in datastream)
                                    {
                                        await WriteChunk(writer, content);
                                    }
                                 }
                                break;
                            default:
                                break;
                        }
                    }
                }

                // Add more chunks as needed

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the request");
                await WriteChunk(writer, "An error occurred while processing your request.");
            }
            finally
            {
                // Ensure to write the final empty chunk to properly end the response
                await writer.WriteAsync("0\r\n\r\n");
                await writer.FlushAsync();
                 if (writer != null)
                {
                    ((IDisposable)writer).Dispose();
                }
            }
        }

        private async Task WriteChunk(StreamWriter writer, string content)
        {
            if (!string.IsNullOrEmpty(content)) {
                string chunk = $"{content.Length:X}\r\n{content}\r\n";
                await writer.WriteAsync(chunk);
                await writer.FlushAsync();
            }
        }

    [HttpGet("jobs/{jobId}/status")]
    public async Task<IActionResult> GetJobStatus(string jobId)
    {
        var jobStatus = new JobStatus
        {
            JobId = jobId,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await Task.Delay(1000);

        return Ok(jobStatus);
    }

    [HttpGet("jobs/{jobId}/results")]
    public async Task<IActionResult> GetJobResults(string jobId)
    {
        var jobResults = new JobResults
        {
            JobId = jobId,
            Results = new List<JobResult>
            {
                new JobResult
                {
                    JobId = "1",
                    Type = "Result 1",
                    Data = new { Value = "Example Value" }
                },
                new JobResult
                {
                     JobId = "1",
                    Type = "Result 1",
                    Data = new { Value = "Example Value" }
                }
            }
        };
        await Task.Delay(1000);

        return Ok(jobResults);
    }
}