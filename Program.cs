using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TourGuideAgentV2.Plugins.Util;
using TourGuideAgentV2.Services;
using TourGuideAgentV2.Plugins.TripPlannerPrompts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var configuration = builder.Configuration;


// Load settings from appsettings.json
string _apiDeploymentName = configuration["AzureOpenAI:DeploymentId"] ?? string.Empty; 
string _apiEndpoint = configuration["AzureOpenAI:Endpoint"] ?? string.Empty; 
string _apiKey = configuration["AzureOpenAI:ApiKey"] ?? string.Empty;

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Add services to the container.

builder.Services.AddControllers();

// Optionally, configure additional logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    // loggingBuilder.AddApplicationInsights(); // Add this line to log to App Insights once you have it setup and set the connectionstring in the appsettings.json file
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add the Semantic Kernel as a transient service
builder.Services.AddTransient<Kernel>(s =>
{
    var builder = Kernel.CreateBuilder();
    builder.AddAzureOpenAIChatCompletion(
        _apiDeploymentName,
        _apiEndpoint,
        _apiKey
    );
    return builder.Build();
});
builder.Services.AddSingleton<IChatCompletionService>(sp =>
    sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());

// Add the ChatHistoryManager as a singleton service to manage chat histories based on client ID
builder.Services.AddSingleton<IChatHistoryManager>(sp =>
{
    string systemmsg = OrchestratorPluginPrompts.GetAgentPrompt();
    return new ChatHistoryManager(systemmsg);
});


// Register the JobResultsCacheService as a singleton
builder.Services.AddSingleton<JobResultsCacheService>(JobResultsCacheService.Instance);

// AddHostedService - ASP.NET will run the ChatHistoryCleanupService in the background and will clean up all chathistores that are older than 1 hour
builder.Services.AddHostedService<ChatHistoryCleanupService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
