using System.Diagnostics;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton<ITelemetryInitializer, EventGridDependencyInitializer>();

var app = builder.Build();

var config = app.Configuration;

var warehouseClient = new HttpClient();
var serviceBus = new ServiceBus(Options.Create(config.GetSection("ServiceBus").Get<ServiceBusSettings>()!));

app.Use(async (context, next) =>
{
    if (context.Features.Get<RequestTelemetry>() is null) throw new Exception("RequestTelemetry Feature is missing. Did you forget to setup App Insights?");
    await next();
});

app.MapGet("/orders/", () => "This is ORDERS service");
app.MapGet("/", () => "This is ORDERS service");
app.MapGet("/orders/echo", () => "This is ORDERS echo");

app.MapPost("/orders/", async (Order order) =>
{
    order.Id = Guid.NewGuid();

    app.Logger.LogDebug("received a new order: {order}", order);

    var response = await warehouseClient.GetAsync("https://localhost:5002/items/");
    System.Console.WriteLine(config["EventGrid:Hostname"]);

    var handler = new HttpClientHandler();

    using var eventGrid = new EventGridClient(new TopicCredentials(config["EventGrid:Key"]));

    var activity = Activity.Current!;

    await eventGrid.PublishEventsAsync(config["EventGrid:Hostname"], new[] {
        new EventGridEvent {
            Id = Guid.NewGuid().ToString(),
            Topic = "orders",
            Data = JObject.FromObject(new {
                Order = order,
                traceparent = activity.TraceParent(),
                Activity = new {
                    activity.RootId,
                    activity.Id,
                    activity.ParentId,
                    activity.ParentSpanId,
                    activity.SpanId,
                    activity.TraceId,
                }
            }),
            EventType = "OrderAccepted",
            Subject = $"orders/{order.Id}",
            DataVersion = "1.0.1"
        }
    });

    return order;
});

app.Run();



public class Order
{
    public Guid Id { get; set; }
    public List<string> Items { get; set; }
}