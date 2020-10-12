// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Sample.Controllers
{
    /// <summary>
    /// Controller for health check api.
    /// </summary>
    [ApiController]
    public class HealthController : ControllerBase
    {
        private readonly IProxyAppState _proxyAppState;

        public HealthController(IProxyAppState proxyAppState)
        {
            _proxyAppState = proxyAppState ?? throw new ArgumentNullException(nameof(proxyAppState));
        }

        /// <summary>
        /// Returns 200 if Proxy is healthy.
        /// </summary>
        [HttpGet]
        [Route("/api/health")]
        public IActionResult CheckHealth()
        {
            return _proxyAppState.IsFullyInitialized ? Ok() : StatusCode(503);
        }
    }
}
