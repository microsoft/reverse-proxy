// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
                Request.Protocol,
                Request.Method,

                Request.Scheme,
                Host = Request.Host.Value,
                Path = Request.Path.Value,
                Query = Request.QueryString.Value,

                Headers = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()),

                Time = DateTimeOffset.UtcNow,
            };

            return Ok(result);
        }

        /// <summary>
        /// Returns a 200 response dumping all info from the incoming request.
        /// </summary>
        [HttpGet]
        [Route("/api/statuscode")]
        public void Status(int statusCode)
        {
            Response.StatusCode = statusCode;
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
                Response.Headers.Add(kvp.Key, kvp.Value);
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
            var bodyReader = Request.BodyReader;
            if (bodyReader != null)
            {
                while (true)
                {
                    var a = await Request.BodyReader.ReadAsync();
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

            var bodyWriter = Response.BodyWriter;
            if (bodyWriter != null && responseSize > 0)
            {
                const int WriteBufferSize = 4096;

                var remaining = responseSize;
                var buffer = new byte[WriteBufferSize];

                while (remaining > 0)
                {
                    buffer[0] = (byte)(remaining * 17); // Make the output not all zeros
                    var toWrite = Math.Min(buffer.Length, remaining);
                    await bodyWriter.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, toWrite), HttpContext.RequestAborted);
                    remaining -= toWrite;
                }
            }
        }
    }
}
