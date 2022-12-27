// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.WebSocketsTelemetry;

internal sealed class HttpUpgradeFeatureWrapper : IHttpUpgradeFeature
{
    private readonly IClock _clock;

    public HttpContext HttpContext { get; private set; }

    public IHttpUpgradeFeature InnerUpgradeFeature { get; private set; }

    public WebSocketsTelemetryStream? TelemetryStream { get; private set; }

    public bool IsUpgradableRequest => InnerUpgradeFeature.IsUpgradableRequest;

    public HttpUpgradeFeatureWrapper(IClock clock, HttpContext httpContext, IHttpUpgradeFeature upgradeFeature)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
        InnerUpgradeFeature = upgradeFeature ?? throw new ArgumentNullException(nameof(upgradeFeature));
    }

    public async Task<Stream> UpgradeAsync()
    {
        Debug.Assert(TelemetryStream is null);
        var opaqueTransport = await InnerUpgradeFeature.UpgradeAsync();

        if (HttpContext.Response.Headers.TryGetValue(HeaderNames.Upgrade, out var upgradeValues) &&
            upgradeValues.Count == 1 &&
            string.Equals("WebSocket", upgradeValues.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            TelemetryStream = new WebSocketsTelemetryStream(_clock, opaqueTransport);
        }

        return TelemetryStream ?? opaqueTransport;
    }
}
