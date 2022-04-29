using System.Diagnostics;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using shipping;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var logger = app.Logger;
var config = app.Configuration;

app.Use(async (context, next) =>
    {
        Activity? activity = null;
        if (context.Request.Headers.ContainsKey("MyOperationId"))
        {
            var operationId = context.Request.Headers["MyOperationId"].ToString();

            activity = new Activity(Activity.Current!.OperationName);
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.SetParentId(operationId);

            activity.Start();
            context.Items["Activity"] = activity;
        }
        try
        {
            await next();
        }
        finally
        {
            activity?.Stop();
        }

    });

app.MapGet("/", () => "This is SHIPPING service");

app.MapPost("/events", async (HttpContext context, ILoggerFactory loggerFactory) =>
    {
        var handler = new EventGridHandler(logger);
        await handler.Handle(context, async gridEvent =>
        {
            logger.LogInformation($"processing grid event: {JsonConvert.SerializeObject(gridEvent)}");

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
app.MapGet("/echo", async context =>
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
        activityDump,
        headers
    }));
});

app.Run();
