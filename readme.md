# TourGuideAgentv2
This example is an ASP.NET MVC API that exposes an AI Chat Streaming endpoint.  It's demonstrating some very important concepts i.e. ChatHistoryManagerService, JobResultsCacheService, Plugins and Function Calling.

## OrchestratorPlugin
This plugin controlls the primary message loop for chat interactions, it makes use of the ChatHistoryManagerService and the JobResultsCacheService and it determine when other Plugins and Functions need to be invoked.

## ChatHistoryManagerSerivce
This is implemented as a Singleton, so it lives as long as the service is running and it's purpose is to cache the chathistory for each client for up to 1hr.  It has a background job that monitors the ChatHistory and purges items that are older than 1hr, so the TTL is 1hr for ChatHistorys that are associated with a ClientID.

This service allows us to cache the ChatHistory for a client across multiple calls to the API, so the content is not lost when the client makes another API call.  The purpose of a cache is to keep these items in memory for 1hr allowing the API to be contextually aware of the conversation for a duration of 1hr.  For example if that shared you name with the endpoint it would recall your name for up to 1hr.

In an Enterprise solution you would combine a Chat History cache with persistent storage i.e. CosmosDB, SQL Postgres etc.

## JobResultsCacheService
There are times in-which the solution may need to execute logic that requires some time to process, so this service allows you to spin up Tasks in the background then store the JobResults in the JobResultsCacheService for later retreival.  This service is very similar to the ChatHistoryManagerService, but is focused on caching JobResults for tasks that need to be executed in the backdround.

## TripPlannerPlugin
All plugins are managed by the OrchestratorPlugin and the Orchestrator Plugin decides when to invoke functions of the Plugin.  I am exposing a function called **SuggestPlaces** in the TripPlannerPlugin.  When the OrchestratorPlugin receives a question from the user it's primary goal is to start streaming a result as quickly as possible but at the same time have the LLM tell us if a Plugin/Function needs to be call and to provide those details to us so the OrchestratorPlugin can make a decision on what to call/invoke.

The idea here is that SuggestPlaces represents a situation that requires some background processing to occur that would introduce latency as a result.  So, the goal is to craft a prompt that allows us to start streaming a response that is not very large and at the end provide a JobId.  When the client sees 'JobId':'12345' at the end of the message it now knows that some work is being done in the background and the client can use the JobId to retrieve the results later.

### Closing Comments
This is one way you could implement an AI Chat Streaming API that allows for long running processes to be completed in the background, and also allowing you to start streaming a response to the client before the background process has been completed.

There is still a little more work that I would like to do in this example, i.e. like actually calling the function (just haven had time to write that logic yet, but it's be easy)

## Example Prompts to play with
### hello, my name is rick and I would your help planning a day trip to Ashville, NC.
This prompt will start an interaction with the API and if set break points you will see that this does not result in the **SuggestPlaces** function being identifying by the LLM that it needs to be involved.

### Can you suggest some points of interests for Ashville, NC?  I am interested in biking and hiking and I will be traveling alone.
Now, this prompt does meet the critira for the LLM to detect that the **SuggestPlaces** function needs to be called.  If you set a breakpoint on line 92 in ChatStreamerController.cs you will see that this line is hit, indicating that the function needs to be called.  

Since the code does not have logic to call the **SuggestPlaces** there is nothing to stream back to the client so you don't get a response.  This is 100% expected with the implementation has I have not added the code for the function calling, but it would look something like this but done in a streaming way.

    ~~~
      functionToCall.InvokeAsync(kernel);
    ~~~

