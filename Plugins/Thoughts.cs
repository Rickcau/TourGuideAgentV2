// Ideas for streaming in the ChatStreamerController.cs
/*
async IAsyncEnumerable<string> StreamFunctionResult(Func<Task<FunctionResultContent>> invokeFunc)
{
    var result = await invokeFunc();
    if (result == null)
    {
        yield return "ServerError: No response from agent";
    }
    else
    {
        // Assuming FunctionResultContent has a property or method to get results as chunks
        foreach (var chunk in result.GetChunks()) // You'd need to implement GetChunks()
        {
            yield return chunk;
        }
    }
}

// Then in your main code:
if (masterOrchestrator.FunctionsToCall)
{
    _logger.LogInformation($"Function calls to be executed: {masterOrchestrator.FunctionsToCall}");
    var functionCalls = masterOrchestrator.GetFunctionsToCall();
    if (functionCalls != null && functionCalls.Any())
    {
        foreach (var func in functionCalls)
        {
            if (func != null)
            {
                Console.WriteLine($"Plugin: {func.PluginName}, Function: {func.FunctionName}, Arguments: {func.Arguments}");
                
                await foreach (var chunk in StreamFunctionResult(() => func.InvokeAsync(_kernel)))
                {
                    await WriteChunk(writer, chunk);
                }
            }
        }
    }
}


// New new approach... 

    public async Task RunAsync()
    {
        Kernel kernel = new();
        KernelFunction streamingFunction = KernelFunctionFactory.CreateFromMethod(StreamingPlugin.StreamingTestAsync);
        FunctionResult result = await kernel.InvokeAsync(streamingFunction);
        IAsyncEnumerable<string> stream = result.GetValue<IAsyncEnumerable<string>>()!;

        await foreach (var content in stream)
        {
            Console.WriteLine($"{DateTime.Now} {content}");
        }
    }

     KernelPlugin plugin = KernelPluginFactory.CreateFromType<StreamingPlugin>();
     kernel.Plugins.Add(plugin);

     KernelFunction f1 = kernel.Plugins.GetFunction("StreamingPlugin", "StreamingTest");
     FunctionResult result = await kernel.InvokeAsync(f1);

    public static class StreamingPlugin
    {
        [KernelFunction]
        public static async IAsyncEnumerable<string> StreamingTestAsync()
        {
            yield return "One";
            await Task.Delay(1000);
            yield return "Two";
            await Task.Delay(1000);
            yield return "Three";
            await Task.Delay(1000);
            yield return "Four";
        }
    }
*/ 

       