// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Yarp.ReverseProxy.WebSocketsTelemetry
{
    internal sealed class WebSocketsTelemetryMiddleware
    {
        private readonly RequestDelegate _next;

        public WebSocketsTelemetryMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public Task Invoke(HttpContext context)
        {
            if (WebSocketsTelemetry.Log.IsEnabled())
            {
                if (context.Features.Get<IHttpUpgradeFeature>() is { IsUpgradableRequest: true } upgradeFeature)
                {
                    return InvokeAsyncCore(context, upgradeFeature, _next);
                }
            }

            return _next(context);
        }

        private static async Task InvokeAsyncCore(HttpContext context, IHttpUpgradeFeature upgradeFeature, RequestDelegate next)
        {
            var upgradeWrapper = new HttpUpgradeFeatureWrapper(upgradeFeature);
            context.Features.Set<IHttpUpgradeFeature>(upgradeWrapper);

            try
            {
                await next(context);
            }
            finally
            {
                if (upgradeWrapper.TelemetryStream is { } telemetryStream)
                {
                    WebSocketsTelemetry.Log.WebSocketClosed(
                        telemetryStream.EstablishedTime.Ticks,
                        telemetryStream.GetCloseReason(context),
                        telemetryStream.MessagesRead,
                        telemetryStream.MessagesWritten);
                }

                context.Features.Set(upgradeFeature);
            }
        }

        private sealed class HttpUpgradeFeatureWrapper : IHttpUpgradeFeature
        {
            private readonly IHttpUpgradeFeature _upgradeFeature;

            public WebSocketsTelemetryStream? TelemetryStream { get; private set; }

            public bool IsUpgradableRequest => _upgradeFeature.IsUpgradableRequest;

            public HttpUpgradeFeatureWrapper(IHttpUpgradeFeature upgradeFeature)
            {
                _upgradeFeature = upgradeFeature ?? throw new ArgumentNullException(nameof(upgradeFeature));
            }

            public async Task<Stream> UpgradeAsync()
            {
                Debug.Assert(TelemetryStream is null);
                var opaqueTransport = await _upgradeFeature.UpgradeAsync();
                TelemetryStream = new WebSocketsTelemetryStream(opaqueTransport);
                return TelemetryStream;
            }
        }
    }
}
