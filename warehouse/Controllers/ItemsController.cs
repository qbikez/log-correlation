using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace warehouse.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ItemsController : ControllerBase
    {
        private readonly ILogger<ItemsController> _logger;
        private readonly IConfiguration config;

        public ItemsController(ILogger<ItemsController> logger, IConfiguration config)
        {
            _logger = logger;
            this.config = config;
        }

        [HttpGet]
        [Route("")]
        public async Task<ActionResult> Get()
        {
            foreach (var header in Request.Headers)
            {
                _logger.LogDebug($"{header.Key}: {header.Value}");
            }
            _logger.LogDebug("returning available items");

            var itemsAvailable = 4;

            using var eventGrid = new EventGridClient(new TopicCredentials(config["EventGrid:Key"]));
            await eventGrid.PublishEventsAsync(config["EventGrid:Hostname"], new List<EventGridEvent>()
            {
                new EventGridEvent()
                {
                    Id = Guid.NewGuid().ToString(),
                        Topic = "items",
                        Data = JObject.FromObject(new
                        {
                            ItemsAvailable = itemsAvailable,
                                Activity = new
                                {
                                    RootId = System.Diagnostics.Activity.Current.RootId,
                                    Id = System.Diagnostics.Activity.Current.Id,
                                    ParentId = System.Diagnostics.Activity.Current.ParentId,
                                    ParentSpanId = System.Diagnostics.Activity.Current.ParentSpanId,
                                    SpanId = System.Diagnostics.Activity.Current.SpanId,
                                    TraceId = System.Diagnostics.Activity.Current.TraceId,
                                }
                        }),
                        EventType = "WarehouseDepleted",
                        Subject = $"warehouse",
                        DataVersion = "1.0.1"
                }
            });

            return Ok(new { avaliable = itemsAvailable });
        }
    }
}