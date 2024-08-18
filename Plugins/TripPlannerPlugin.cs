using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TourGuideAgentV2.Models;
using TourGuideAgentV2.Plugins.TripPlannerPrompts;
using TourGuideAgentV2.Services;

namespace TourGuideAgentV2.Plugins.TripPlanner
{
    internal class TripPlannerPlugin
    {
        private IChatCompletionService _chatService;
        private IChatHistoryManager _chatHistoryManager;

        private readonly JobResultsCacheService _jobResultsCacheService;
        private readonly Kernel _kernel;
        public TripPlannerPlugin(IChatCompletionService chatService, IChatHistoryManager chathistorymanager, JobResultsCacheService cacheservice, Kernel kernel)
        {
            _chatService = chatService;
            _chatHistoryManager = chathistorymanager;
            _jobResultsCacheService = cacheservice;
            _kernel = kernel.Clone();  // Let's clone the kernel so we have a fresh copy to work with with zero plugins registered
        }

        // [Microsoft.SemanticKernel.KernelFunction, Description("Generate an road trip itinerary")]
        // public async Task<string> SuggestRoadtrip(
        //     [Description("Source city"), Required] string source,
        //     [Description("Destination city"), Required] string destination,
        //     [Description("User preffered categories"), Required] string categories,
        //     [Description("Travel companions if user traveling alone, with family, or with a group?"), Required] string travelCompanions
        // )
        // {
        //     var executionSettings = new OpenAIPromptExecutionSettings
        //     {
        //         ResponseFormat = "json_object"
        //     };

        //     // Keep the ChatHistory local since we only need it to detect the Intent
        //     ChatHistory chatHistory = new ChatHistory();
        //     chatHistory.AddSystemMessage(TravelPluginPrompts.GetRoadtripPrompt(source, destination, categories, travelCompanions));
            
        //     var result = await this._chatService.GetChatMessageContentAsync(chatHistory, executionSettings);
            
        //     return result.ToString();
        // }
        
        [Microsoft.SemanticKernel.KernelFunction, Description("Suggest points of interests and highlights in given location")]
        public async IAsyncEnumerable<string> SuggestPlaces(
            [Description("Location"), Required] string location,
            [Description("User prefered categories"), Required] string categories,
            [Description("Travel companions if user traveling alone, with family, or with a group?"), Required] string travelCompanions
        )
        {
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ResponseFormat = "json_object"
            };

            // Keep the ChatHistory local since we only need it to detect the Intent
            ChatHistory localChatHistory = new ChatHistory();
            localChatHistory.AddSystemMessage(TripPlannerPluginPrompts.GetPlacesPrompt(location,categories,travelCompanions));

            var assistantResponse = "";
            await foreach (var chatUpdate in _chatService.GetStreamingChatMessageContentsAsync(localChatHistory, executionSettings,_kernel))
            {      
                   assistantResponse += chatUpdate.ToString();          
                   yield return chatUpdate.ToString();
            }
            localChatHistory.AddSystemMessage(assistantResponse); // Not really needed at this point but might need it later, logic is not finished here.

            // Start the background task to process the trip and add the result to the cache
            // you would then need to expose an API to retrieve the results from the cache, the client would call this 
            var jobId = Guid.NewGuid().ToString();  // generate the JobId
            yield return $" JobId: {jobId}"; // return the JobId to the client
            var result = StartBackgroundTripProcessing(jobId, location, categories, travelCompanions);
        } 

        public string StartBackgroundTripProcessing(string jobId, string location, string categories, string travelCompanions)
        {
            // Start the background task
            _ = Task.Run(async () =>
            {
                // Simulate some work
                await Task.Delay(5000); // Simulate 5 seconds of work
                var jobResults = new JobResults
                {
                    JobId = jobId,
                    Results = new List<JobResult>
                    {
                        new JobResult
                        {
                            JobId = jobId,
                            Type = "Result 1",
                            Data = new { Value = "Example Value" }
                        },
                        new JobResult
                        {
                            JobId = jobId,
                            Type = "Result 1",
                            Data = new { Value = "Example Value" }
                        }
                    }
                };

                // Store the results in the cache
                _jobResultsCacheService.StoreJobResults(jobId, jobResults);

                Console.WriteLine($"Background task completed for job {jobId}");
            });

            return $"Background task started with job ID: {jobId}";
        }         
    }
}



