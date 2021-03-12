// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Yarp.Sample.Controllers
{
    /// <summary>
    /// Sample controller.
    /// </summary>
    [ApiController]
    public class HttpController : ControllerBase
    {
        /// <summary>
        /// Returns a 200 response dumping all info from the incoming request.
        /// </summary>
        [HttpGet, HttpPost]
        [Route("/api/dump")]
        public IActionResult Dump()
        {
            var result = new {
                Request.Protocol,
                Request.Method,
                Request.Scheme,
                Host = Request.Host.Value,
                PathBase = Request.PathBase.Value,
                Path = Request.Path.Value,
                Query = Request.QueryString.Value,
                Headers = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray()),
                Time = DateTimeOffset.UtcNow
            };

            return Ok(result);
        }
    }
}
