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

                await foreach (var chunk in masterOrchestrator.MessageLoopStreamAsync(request.Prompt, _clientId, _vehicleId!))
                {
                    if (chunk == null)
                    {
                        await WriteChunk(writer, "ServerError: No response from agent");
                    }
                    await WriteChunk(writer, chunk!.ToString());
                    //await Task.Delay(200);
                }

                if (masterOrchestrator.FunctionsToCall)
                {
                   _logger.LogInformation($"Function calls to be executed:{ masterOrchestrator.FunctionsToCall}");
                   var functionCalls = masterOrchestrator.GetFunctionsToCall();
                    if (functionCalls != null && functionCalls.Any())
                    {

                        foreach (var func in functionCalls)
                        {
                            if (func != null)
                            {
                                Console.WriteLine($"Plugin: {func.PluginName}, Function: {func.FunctionName}, Arguments: {func.Arguments}");
                            }

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