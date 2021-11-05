// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

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
        if (UseOriginalHost)
        {
            if (!context.HeadersCopied)
            {
                // Don't override a custom host
                context.ProxyRequest.Headers.Host ??= context.HttpContext.Request.Host.Value;
            }
        }
        else if (context.HeadersCopied
            // Don't remove a custom host, only the original
            && string.Equals(context.HttpContext.Request.Host.Value, context.ProxyRequest.Headers.Host, StringComparison.Ordinal))
        {
            // Remove it after the copy, use the destination host instead.
            context.ProxyRequest.Headers.Host = null;
        }

        return default;
    }
}
