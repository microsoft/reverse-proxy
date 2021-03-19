// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc;

namespace Yarp.Sample.Controllers
{
    /// <summary>
    /// Controller for health check api.
    /// </summary>
    [ApiController]
    public class HealthController : ControllerBase
    {
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
