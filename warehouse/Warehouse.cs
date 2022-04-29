using System.Diagnostics;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Newtonsoft.Json.Linq;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var _logger = app.Logger;
var config = app.Configuration;

app.MapGet("/", () => "This is WAREHOUSE service");

app.MapGet("/items", async (HttpRequest request) =>
{
    foreach (var header in request.Headers)
    {
        _logger.LogDebug($"{header.Key}: {header.Value}");
    }
    _logger.LogDebug("returning available items");

    var itemsAvailable = 4;

    using var eventGrid = new EventGridClient(new TopicCredentials(config["EventGrid:Key"]));

    var activity = Activity.Current!;

    await eventGrid.PublishEventsAsync(config["EventGrid:Hostname"], new List<EventGridEvent>()
        {
            new EventGridEvent()
            {
                Id = Guid.NewGuid().ToString(),
                    Topic = "items",
                    Data = JObject.FromObject(new
                    {
                        traceparent = activity.TraceParent(),
                        ItemsAvailable = itemsAvailable,
                        Activity = new
                        {
                            activity.RootId,
                            activity.Id,
                            activity.ParentId,
                            activity.ParentSpanId,
                            activity.SpanId,
                            activity.TraceId,
                        }
                    }),
                    EventType = "WarehouseDepleted",
                    Subject = $"warehouse",
                    DataVersion = "1.0.1"
            }
        });

    return new { avaliable = itemsAvailable };
});

app.Run();


public class CloudRoleNameTelemetryInitializer : ITelemetryInitializer
{
    public readonly string roleName;
    public CloudRoleNameTelemetryInitializer(string roleName)
    {
        this.roleName = roleName;
    }
    public void Initialize(ITelemetry telemetry)
    {
        // set custom role name here
        telemetry.Context.Cloud.RoleName = this.roleName;
    }
}

public class EventGridDependencyInitializer : ITelemetryInitializer
{
    public EventGridDependencyInitializer()
    {
    }
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is DependencyTelemetry dependency)
        {
            var activity = Activity.Current!;
            var id = activity.GetBaggageItem("next_spanId");
            if (!string.IsNullOrEmpty(id))
            {
                dependency.Id = id;
                dependency.Type = "Azure Service Bus";
            }
        }

    }
}

public static class ActivityExtensions
{
    public static string? TraceParent(this Activity activity)
    {
        if (activity?.SpanId == null || activity?.Id == null) return null;

        var nextSpanId = ActivitySpanId.CreateRandom().ToHexString();
        activity.AddBaggage("next_spanId", nextSpanId);

        var currentSpanId = activity.SpanId.ToHexString();
        var traceparent = activity.Id.Replace(currentSpanId, nextSpanId);

        return traceparent;
    }
}