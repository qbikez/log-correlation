using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace warehouse.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ItemsController : ControllerBase
    {
        private readonly ILogger<ItemsController> _logger;

        public ItemsController(ILogger<ItemsController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("")]
        public ActionResult Get()
        {
            foreach(var header in Request.Headers) {
                _logger.LogDebug($"{header.Key}: {header.Value}");
            }
            _logger.LogDebug("returning available items");
            return Ok(new { avaliable = 4 });
        }
    }
}
