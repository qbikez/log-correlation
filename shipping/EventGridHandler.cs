using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace shipping;

class EventGridHandler
{
    private readonly ILogger log;

    public EventGridHandler(ILogger log)
    {
        this.log = log;
    }

    public async Task Handle(HttpContext context, Func<EventGridEvent, Task> callback)
    {
        string response = string.Empty;

        using var reader = new StreamReader(context.Request.Body);
        var requestContent = await reader.ReadToEndAsync();
        log.LogDebug($"Received events: {requestContent}");

        EventGridSubscriber eventGridSubscriber = new EventGridSubscriber();

        EventGridEvent[] eventGridEvents = JsonConvert.DeserializeObject<EventGridEvent[]>(requestContent);

        var validationEvent = eventGridEvents.FirstOrDefault(e => e.EventType == "Microsoft.EventGrid.SubscriptionValidationEvent");

        if (validationEvent != null)
        {
            await InActivityContext(context, validationEvent, async() =>
            {
                var result = HandleValidation(validationEvent);

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonConvert.SerializeObject(result));
            });
            return;
        }

        foreach (EventGridEvent eventGridEvent in eventGridEvents)
        {
            await InActivityContext(context, eventGridEvent, () => callback(eventGridEvent));
        }

        context.Response.StatusCode = 202;
    }

    private static async Task InActivityContext(HttpContext context, EventGridEvent eventGridEvent, Func<Task> callback)
    {
        var activity = new Activity($"EVENT {eventGridEvent.EventType} {eventGridEvent.Subject}");
        activity.SetParentId(GetOperationId(eventGridEvent));
        activity.Start();

        var requestTelemetry = context.Features.Get<RequestTelemetry>();
        if (requestTelemetry is null) throw new NullReferenceException("request telemetry is null. Did you configure app insights?")
        
        var operation = requestTelemetry.Context.Operation;
        requestTelemetry.Name = operation.Name;
        requestTelemetry.Id = activity.SpanId.ToHexString();
        
        operation.Name = activity.OperationName;
        operation.Id = activity.TraceId.ToHexString();
        operation.ParentId = activity.ParentSpanId.ToHexString();

        try
        {
            await callback();
        }
        finally
        {
            activity.Stop();
        }
    }

    private static string GetOperationId(EventGridEvent eventGridEvent)
    {
        string operationId = null;
        try
        {
            if (((JObject)eventGridEvent.Data).TryGetValue("traceparent", out var traceParentValue))
            {
                operationId = traceParentValue.Value<string>();
            }
        }
        catch
        {
            // ignore
        }
        return operationId;
    }

    private object HandleValidation(EventGridEvent validationEvent)
    {
        var eventData = ((JObject)validationEvent.Data).ToObject<SubscriptionValidationEventData>();
        log.LogInformation($"Got SubscriptionValidation event data, validation code: {eventData.ValidationCode}, topic: {validationEvent.Topic}");
        // Do any additional validation (as required) and then return back the below response

        var responseData = new SubscriptionValidationResponse()
        {
            ValidationResponse = eventData.ValidationCode
        };

        return responseData;
    }
}