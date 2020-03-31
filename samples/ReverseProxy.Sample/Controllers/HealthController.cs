// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Sample.Controllers
{
    /// <summary>
    /// Controller for health check api.
    /// </summary>
    [ApiController]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthController"/> class.
        /// </summary>
        public HealthController(ILogger<HealthController> logger)
        {
            Contracts.CheckValue(logger, nameof(logger));
            _logger = logger;
        }

        /// <summary>
        /// Returns 200 if Proxy is healthy.
        /// </summary>
        [HttpGet]
        [Route("/api/health")]
        public IActionResult CheckHealth()
        {
            // TODO: Implement health controller, use guid in route.
            return Ok();
        }
    }
}
