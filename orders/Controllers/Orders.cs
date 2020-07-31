﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Newtonsoft.Json.Linq;

namespace orders_backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly ILogger<OrdersController> _logger;
        private readonly IConfiguration config;
        private static HttpClient warehouseClient = new HttpClient();

        public OrdersController(ILogger<OrdersController> logger, IConfiguration config)
        {
            _logger = logger;
            this.config = config;
        }

        [HttpPost]
        [Route("")]
        public async Task<ActionResult> Order([FromBody] Order order)
        {
            order.Id = Guid.NewGuid();

            // _logger.LogDebug($"received a new order: {order}");
            _logger.LogDebug("received a new order: {order}", order);

            var response = await warehouseClient.GetAsync("https://localhost:5002/items/");
            System.Console.WriteLine(config["EventGrid:Hostname"]);
            using var eventGrid = new EventGridClient(new TopicCredentials(config["EventGrid:Key"]));
            await eventGrid.PublishEventsAsync(config["EventGrid:Hostname"], new List<EventGridEvent>() {
                new EventGridEvent() {
                    Id = Guid.NewGuid().ToString(),
                    Topic = "orders",
                    Data = JObject.FromObject(new {
                        Order = order,
                        Activity = new {
                            RootId = System.Diagnostics.Activity.Current.RootId,
                            Id = System.Diagnostics.Activity.Current.Id,
                            ParentId = System.Diagnostics.Activity.Current.ParentId,
                            ParentSpanId = System.Diagnostics.Activity.Current.ParentSpanId,
                            SpanId = System.Diagnostics.Activity.Current.SpanId,
                            TraceId = System.Diagnostics.Activity.Current.TraceId,
                        }
                    }),
                    EventType = "OrderAccepted",
                    Subject = $"orders/{order.Id}",
                    DataVersion = "1.0.1"
                }
            });
            return Ok(order);
        }
    }

    public class Order
    {
        public Guid Id { get; set; }
        public List<string> Items {get;set;}
    }
}