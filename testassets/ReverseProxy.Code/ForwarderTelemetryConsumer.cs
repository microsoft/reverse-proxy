// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Yarp.ReverseProxy.Sample
{
    public sealed class ForwarderTelemetryConsumer : IForwarderTelemetryConsumer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AsyncLocal<DateTime?> _startTime = new();

        public ForwarderTelemetryConsumer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void OnForwarderStart(DateTime timestamp, string destinationPrefix)
        {
            _startTime.Value = timestamp;
        }

        public void OnForwarderStop(DateTime timestamp, int statusCode)
        {
            if (_startTime.Value is DateTime startTime)
            {
                var elapsed = timestamp - startTime;
                var path = _httpContextAccessor.HttpContext.Request.Path;
                Console.WriteLine($"Spent {elapsed.TotalMilliseconds:N2} ms proxying {path}");
            }
        }
    }
}
