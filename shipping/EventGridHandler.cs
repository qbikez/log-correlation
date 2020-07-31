using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
            log.LogInformation($"C# HTTP trigger function began");
            string response = string.Empty;

            using var reader = new StreamReader(context.Request.Body);
            var requestContent = await reader.ReadToEndAsync();
            log.LogInformation($"Received events: {requestContent}");

            EventGridSubscriber eventGridSubscriber = new EventGridSubscriber();

            EventGridEvent[] eventGridEvents = eventGridSubscriber.DeserializeEventGridEvents(requestContent);

            var validationEvent = eventGridEvents.FirstOrDefault(e => e.Data is SubscriptionValidationEventData);

            if (validationEvent != null)
            {
                var result = HandleValidation(validationEvent);
                
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonConvert.SerializeObject(result));

                return;
            }

            foreach (EventGridEvent eventGridEvent in eventGridEvents)
            {
                await callback(eventGridEvent);
            }

            context.Response.StatusCode = 202;
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