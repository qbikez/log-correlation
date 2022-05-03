using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using shipping;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton<ITelemetryInitializer, EventGridDependencyInitializer>();

var app = builder.Build();

var logger = app.Logger;
var config = app.Configuration;
var client = new HttpClient();

var serviceBusClient = new ServiceBusClient(config["ServiceBus:ConnectionString"]);
var queueProcessor = serviceBusClient.CreateSessionProcessor(config["ServiceBus:Topic"], config["ServiceBus:Subscription"], new ServiceBusSessionProcessorOptions() {
    AutoCompleteMessages = true,    
});

queueProcessor.ProcessMessageAsync += async (e) => {
    var message = e.Message;
    // we don't need to create a new activity, because the processor already did it
    var activity = Activity.Current!;
    if (message.ApplicationProperties.TryGetValue("Diagnostic-Id", out var objectId) && objectId is string diagnosticId)
        activity.SetTraceParent(diagnosticId);
    logger.LogInformation($"processing queue message {message.Subject}");
    var echoResponse = await client.GetAsync("https://localhost:5001/orders/echo?msg=processing_queue_message");
};

queueProcessor.ProcessErrorAsync += async (e) => {    
    logger.LogError(e.Exception, $"error when processing queue message {e}");
};

await queueProcessor.StartProcessingAsync();

app.Use(async (context, next) => {
    if (context.Features.Get<RequestTelemetry>() is null) throw new Exception("RequestTelemetry Feature is missing. Did you forget to setup App Insights?");
    await next();
});

app.MapGet("/shipping", () => "This is SHIPPING service");
app.MapGet("/shipping/echo", () => "This is SHIPPING echo");

app.MapPost("/shipping/events", async (HttpContext context, ILoggerFactory loggerFactory) =>
    {
        var telemetry = context.Features.Get<Microsoft.ApplicationInsights.DataContracts.RequestTelemetry>();
        var handler = new EventGridHandler(logger);
        await handler.Handle(context, async gridEvent =>
        {
            logger.LogInformation($"processing grid event: {JsonConvert.SerializeObject(gridEvent)}");

            var echoResponse = await client.GetAsync("https://localhost:5001/orders/echo/?msg=processing_event");

            if (gridEvent.EventType == "WarehouseDepleted")
            {
                logger.LogInformation("sending another event");
                using var eventGrid = new EventGridClient(new TopicCredentials(config["EventGrid:Key"]));
                await eventGrid.PublishEventsAsync(config["EventGrid:Hostname"], new List<EventGridEvent>()
                {
                    new EventGridEvent()
                    {
                        Id = Guid.NewGuid().ToString(),
                            Topic = "shipping",
                            Data = JObject.FromObject(new
                            {
                                traceparent = Activity.Current!.TraceParent(),
                            }),
                            EventType = "ItemShipped",
                            Subject = $"shipping",
                            DataVersion = "1.0.1"
                    }
                });
            }

            await Task.Yield();
        });
    });

app.MapGet("/shipping/echo", async context =>
{
    var activity = Activity.Current!;
    var headers = context.Request.Headers;
    var activityDump = new
    {
        activity.RootId,
        activity.Id,
        activity.ParentId,
        activity.ParentSpanId,
        activity.SpanId,
        activity.TraceId,
    };

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(JsonConvert.SerializeObject(new
    {
        activity = activityDump,
        headers
    }));
});

app.Run();
