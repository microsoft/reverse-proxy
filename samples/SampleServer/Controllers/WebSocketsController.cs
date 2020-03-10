// <copyright file="WebSocketsController.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
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
    public class WebSocketsController : ControllerBase
    {
        private readonly ILogger<WebSocketsController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketsController"/> class.
        /// </summary>
        public WebSocketsController(ILogger<WebSocketsController> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Returns a 200 response.
        /// </summary>
        [HttpGet]
        [Route("/api/websockets")]
        public async Task WebSockets()
        {
            if (!this.HttpContext.WebSockets.IsWebSocketRequest)
            {
                this.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }

            using (var webSocket = await this.HttpContext.WebSockets.AcceptWebSocketAsync())
            {
                this.logger.LogInformation("WebSockets established.");
                await this.RunPingPongAsync(webSocket, this.HttpContext.RequestAborted);
            }

            this.logger.LogInformation("WebSockets finished.");
        }

        private async Task RunPingPongAsync(WebSocket webSocket, CancellationToken cancellation)
        {
            var buffer = new byte[1024];
            while (true)
            {
                var message = await webSocket.ReceiveAsync(buffer, cancellation);
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Bye", cancellation);
                    return;
                }

                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, message.Count), message.MessageType, message.EndOfMessage, cancellation);
            }
        }
    }
}
