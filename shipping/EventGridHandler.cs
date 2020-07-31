using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace shipping
{
    class EventGridHandler
    {
        private readonly ILogger log;

        public EventGridHandler(ILogger<EventGridHandler> log)
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

            var validationEvent = eventGridEvents.FirstOrDefault(e => e.Data is SubscriptionValidationEventData);

            if (validationEvent != null)
            {
                SetOperationId(context, validationEvent);
                var result = HandleValidation(validationEvent);

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonConvert.SerializeObject(result));

                return;
            }

            foreach (EventGridEvent eventGridEvent in eventGridEvents)
            {
                InActivityContext(context, eventGridEvent, callback);
            }

            context.Response.StatusCode = 202;
        }

        private static void InActivityContext(HttpContext context, EventGridEvent eventGridEvent, Func<EventGridEvent, Task> callback)
        {
            Activity activity = new Activity($"{eventGridEvent.EventType} {eventGridEvent.Subject}");
            SetOperationId(context, eventGridEvent, activity);
            try
            {
                callback(eventGridEvent);
            }
            finally
            {
                activity.Stop();
            }
        }

        private static void SetOperationId(HttpContext context, EventGridEvent eventGridEvent, Activity activity = null)
        {
            try
            {
                string operationId = null;
                if (((JObject)eventGridEvent.Data).TryGetValue("traceparent", out var traceParentValue))
                {
                    operationId = traceParentValue.Value<string>();
                }

                if (!string.IsNullOrEmpty(operationId))
                {
                    context.Items["OperationId"] = operationId;

                    activity?.SetParentId(operationId);
                }
            }
            catch
            {
                // ignore
            }
        }

        private object HandleValidation(EventGridEvent validationEvent)
        {
            var eventData = (SubscriptionValidationEventData)validationEvent.Data;
            log.LogInformation($"Got SubscriptionValidation event data, validation code: {eventData.ValidationCode}, topic: {validationEvent.Topic}");
            // Do any additional validation (as required) and then return back the below response

            var responseData = new SubscriptionValidationResponse()
            {
                ValidationResponse = eventData.ValidationCode
            };

            return responseData;
        }
    }
}