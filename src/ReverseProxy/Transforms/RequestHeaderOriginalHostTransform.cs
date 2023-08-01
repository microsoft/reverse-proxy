// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// A transform used to include or suppress the original request host header.
/// </summary>
public class RequestHeaderOriginalHostTransform : RequestTransform
{
    public static readonly RequestHeaderOriginalHostTransform OriginalHost = new(true);

    public static readonly RequestHeaderOriginalHostTransform SuppressHost = new(false);

    /// <summary>
    /// Creates a new <see cref="RequestHeaderOriginalHostTransform"/>.
    /// </summary>
    /// <param name="useOriginalHost">True of the original request host header should be used,
    /// false otherwise.</param>
    private RequestHeaderOriginalHostTransform(bool useOriginalHost)
    {
        UseOriginalHost = useOriginalHost;
    }

    internal bool UseOriginalHost { get; }

    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        var destinationConfigHost = context.HttpContext.Features.Get<IReverseProxyFeature>()?.ProxiedDestination?.Model.Config?.Host;
        var originalHost = context.HttpContext.Request.Host.Value is { Length: > 0 } host ? host : null;
        var existingHost = RequestUtilities.TryGetValues(context.ProxyRequest.Headers, HeaderNames.Host, out var currentHost) ? currentHost.ToString() : null;

        if (UseOriginalHost)
        {
            if (!context.HeadersCopied && existingHost is null)
            {
                // Propagate the host if the transform pipeline didn't already override it.
                // If there was no original host specified, allow the destination config host to flow through.
                context.ProxyRequest.Headers.TryAddWithoutValidation(HeaderNames.Host, originalHost ?? destinationConfigHost);
            }
        }
        else if (((context.HeadersCopied && existingHost is not null && string.Equals(originalHost, existingHost, StringComparison.Ordinal))
            || (!context.HeadersCopied && existingHost is null)))
        {
            // Either headers were copied, there is a host, and it's equal to the original host (i.e, unchanged),
            // Or, headers weren't copied and there is no existing host.
            // Suppress the original host, setting the host to the destination host (which may be null).
            context.ProxyRequest.Headers.Host = destinationConfigHost;
        }

        return default;
    }
}
