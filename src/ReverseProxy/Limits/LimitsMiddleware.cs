// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Limits;

/// <summary>
/// Updates request limits based on route config. This is implemented as middleware at the end of the proxy
/// pipeline so that apps can call ReassignProxyRequest to move the request to a different route before limits are applied.
/// This may be replaced in the future by route metadata and an aspnetcore middleware https://github.com/dotnet/aspnetcore/issues/40452.
/// </summary>
internal sealed class LimitsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public LimitsMiddleware(RequestDelegate next, ILogger<LimitsMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task Invoke(HttpContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));

        var config = context.GetRouteModel().Config;

        if (config.MaxRequestBodySize.HasValue)
        {
            var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeFeature != null && !sizeFeature.IsReadOnly)
            {
                // -1 for disabled
                var limit = config.MaxRequestBodySize.Value;
                long? newValue = limit == -1 ? null : limit;
                sizeFeature.MaxRequestBodySize = newValue;
                Log.MaxRequestBodySizeSet(_logger, limit);
            }
        }

        return _next(context);
    }

    private static class Log
    {
        private static readonly Action<ILogger, long?, Exception?> _maxRequestBodySizeSet = LoggerMessage.Define<long?>(
            LogLevel.Debug,
            EventIds.MaxRequestBodySizeSet,
            "The MaxRequestBodySize has been set to '{limit}'.");

        public static void MaxRequestBodySizeSet(ILogger logger, long? limit)
        {
            _maxRequestBodySizeSet(logger, limit, null);
        }
    }
}
