// <copyright file="HealthController.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.CoreServicesBorrowed;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IslandGateway.Sample.Controllers
{
    /// <summary>
    /// Controller for health check api.
    /// </summary>
    [ApiController]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthController"/> class.
        /// </summary>
        public HealthController(ILogger<HealthController> logger)
        {
            Contracts.CheckValue(logger, nameof(logger));
            this.logger = logger;
        }

        /// <summary>
        /// Returns 200 if Gateway is healthy.
        /// </summary>
        [HttpGet]
        [Route("/api/health")]
        public IActionResult CheckHealth()
        {
            // TODO: Implement health controller, use guid in route.
            return this.Ok();
        }
    }
}
