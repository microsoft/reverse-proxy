// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Utilities;
using Yarp.ReverseProxy.WebSocketsTelemetry;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// <see cref="IApplicationBuilder"/> extension methods to add the <see cref="WebSocketsTelemetryMiddleware"/>.
/// </summary>
public static class WebSocketsTelemetryExtensions
{
    /// <summary>
    /// Adds a <see cref="WebSocketsTelemetryMiddleware"/> to the request pipeline.
    /// Must be added before <see cref="WebSockets.WebSocketMiddleware"/>.
    /// </summary>
    public static IApplicationBuilder UseWebSocketsTelemetry(this IApplicationBuilder app)
    {
        return app.Use(next =>
        {
                // Avoid exposing another extension method (AddWebSocketsTelemetry) just because of IClock
                var clock = app.ApplicationServices.GetServices<IClock>().FirstOrDefault() ?? new Clock();
            return new WebSocketsTelemetryMiddleware(next, clock).InvokeAsync;
        });
    }
}
