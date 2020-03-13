// <copyright file="UpgradeController.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SampleServer.Controllers
{
    /// <summary>
    /// Sample controller.
    /// </summary>
    [ApiController]
    public class UpgradeController : ControllerBase
    {
        private readonly ILogger<UpgradeController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpgradeController"/> class.
        /// </summary>
        public UpgradeController(ILogger<UpgradeController> logger)
        {
            this._logger = logger;
        }

        /// <summary>
        /// Upgrades the connection to a raw socket stream, then implements a simple byte ping/pong server.
        /// Note that this does not use WebSockets, and relies solely on HTTP/1.1 connection upgrade mechanism.
        /// </summary>
        [HttpGet]
        [Route("/api/rawupgrade")]
        public async Task RawUpgrade()
        {
            var upgradeFeature = this.HttpContext.Features.Get<IHttpUpgradeFeature>();
            if (upgradeFeature != null && upgradeFeature.IsUpgradableRequest)
            {
                using (var stream = await upgradeFeature.UpgradeAsync())
                {
                    this._logger.LogInformation("Upgraded connection.");
                    await this.RunPingPongAsync(stream);
                    this._logger.LogInformation("Finished.");
                }
            }
            else
            {
                this.HttpContext.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            }
        }

        /// <summary>
        /// Simple echo protocol that echo's each received byte.
        /// <c>255</c> is treated as a special "goodbye" message, which causes us to drop the connection.
        /// </summary>
        private async Task RunPingPongAsync(Stream stream)
        {
            var buffer = new byte[1];
            int read;
            while ((read = await stream.ReadAsync(buffer, this.HttpContext.RequestAborted)) != 0)
            {
                if (buffer[0] == 255)
                {
                    // Goodbye
                    break;
                }

                await stream.WriteAsync(buffer, 0, read);
            }
        }
    }
}
