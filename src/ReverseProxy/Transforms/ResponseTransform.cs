// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms;

/// <summary>
/// Transforms for responses.
/// </summary>
public abstract class ResponseTransform
{
    /// <summary>
    /// Transforms the given response. The status and headers will have (optionally) already been
    /// copied to the <see cref="HttpResponse"/> and any changes should be made there.
    /// </summary>
    public abstract ValueTask ApplyAsync(ResponseTransformContext context);

    /// <summary>
    /// Removes and returns the current header value by first checking the HttpResponse
    /// and falling back to the value from HttpResponseMessage or HttpContent only if
    /// <see cref="ResponseTransformContext.HeadersCopied"/> is not set.
    /// This ordering allows multiple transforms to mutate the same header.
    /// </summary>
    /// <param name="context">The transform context.</param>
    /// <param name="headerName">The name of the header to take.</param>
    /// <returns>The response header value, or StringValues.Empty if none.</returns>
    public static StringValues TakeHeader(ResponseTransformContext context, string headerName)
    {
        if (context is null)
        {
            throw new System.ArgumentNullException(nameof(context));
        }

        if (string.IsNullOrEmpty(headerName))
        {
            throw new System.ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
        }

        var existingValues = StringValues.Empty;

        if (context.HttpContext.Response.Headers.TryGetValue(headerName, out var responseValues))
        {
            context.HttpContext.Response.Headers.Remove(headerName);
            existingValues = responseValues;
        }
        else if (context.ProxyResponse != null && !context.HeadersCopied
            && (context.ProxyResponse.Headers.TryGetValues(headerName, out var values)
                || context.ProxyResponse.Content.Headers.TryGetValues(headerName, out values)))
        {
            existingValues = (string[])values;
        }

        return existingValues;
    }

    /// <summary>
    /// Sets the given header on the HttpResponse.
    /// </summary>
    public static void SetHeader(ResponseTransformContext context, string headerName, StringValues values)
    {
        context.HttpContext.Response.Headers[headerName] = values;
    }

    internal static bool Success(ResponseTransformContext context)
    {
        // TODO: How complex should this get? Compare with http://nginx.org/en/docs/http/ngx_http_headers_module.html#add_header
        return context.HttpContext.Response.StatusCode < 400;
    }
}
