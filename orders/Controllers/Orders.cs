using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace orders_backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly ILogger<OrdersController> _logger;
        private static HttpClient warehouseClient = new HttpClient();

        public OrdersController(ILogger<OrdersController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        [Route("")]
        public async Task<ActionResult> Order([FromBody] Order order)
        {
            order.Id = Guid.NewGuid();

            // _logger.LogDebug($"received a new order: {order}");
            _logger.LogDebug("received a new order: {order}", order);

            var response = await warehouseClient.GetAsync("https://localhost:5002/items/");
            
            return Ok(order);
        }
    }

    public class Order
    {
        public Guid Id { get; set; }
        public List<string> Items {get;set;}
    }
}