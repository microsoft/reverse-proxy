// <copyright file="HttpController.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace SampleServer.Controllers
{
    /// <summary>
    /// Sample controller.
    /// </summary>
    [ApiController]
    public class HttpController : ControllerBase
    {
        /// <summary>
        /// Returns a 200 response.
        /// </summary>
        [HttpGet]
        [Route("/api/noop")]
        public void NoOp()
        {
        }

        /// <summary>
        /// Returns a 200 response dumping all info from the incoming request.
        /// </summary>
        [HttpGet]
        [Route("/api/dump")]
        public IActionResult Dump()
        {
            var result = new
            {
                this.Request.Protocol,
                this.Request.Method,

                this.Request.Scheme,
                Host = this.Request.Host.Value,
                Path = this.Request.Path.Value,
                Query = this.Request.QueryString.Value,

                Headers = this.Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()),

                Time = DateTimeOffset.UtcNow,
            };

            return this.Ok(result);
        }

        /// <summary>
        /// Returns a 200 response dumping all info from the incoming request.
        /// </summary>
        [HttpGet]
        [Route("/api/statuscode")]
        public void Status(int statusCode)
        {
            this.Response.StatusCode = statusCode;
        }

        /// <summary>
        /// Returns a 200 response dumping all info from the incoming request.
        /// </summary>
        [HttpGet]
        [Route("/api/headers")]
        public void Headers([FromBody] Dictionary<string, string> headers)
        {
            foreach (var kvp in headers)
            {
                this.Response.Headers.Add(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Returns a 200 response after <paramref name="delay"/> milliseconds
        /// and containing with <paramref name="responseSize"/> bytes in the response body.
        /// </summary>
        [HttpGet]
        [HttpPut]
        [HttpPost]
        [HttpPatch]
        [Route("/api/stress")]
        public async Task Stress([FromQuery]int delay, [FromQuery]int responseSize)
        {
            var bodyReader = this.Request.BodyReader;
            if (bodyReader != null)
            {
                while (true)
                {
                    var a = await this.Request.BodyReader.ReadAsync();
                    if (a.IsCompleted)
                    {
                        break;
                    }
                }
            }

            if (delay > 0)
            {
                await Task.Delay(delay);
            }

            var bodyWriter = this.Response.BodyWriter;
            if (bodyWriter != null && responseSize > 0)
            {
                const int WriteBufferSize = 4096;

                int remaining = responseSize;
                var buffer = new byte[WriteBufferSize];

                while (remaining > 0)
                {
                    buffer[0] = (byte)(remaining * 17); // Make the output not all zeros
                    int toWrite = Math.Min(buffer.Length, remaining);
                    await bodyWriter.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, toWrite), this.HttpContext.RequestAborted);
                    remaining -= toWrite;
                }
            }
        }
    }
}
