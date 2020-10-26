// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc;

namespace Microsoft.ReverseProxy.Sample.Controllers
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
            return Ok();
        }
    }
}
