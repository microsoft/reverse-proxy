// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Forwarder;

public class HttpTransformer
{
    /// <summary>
    /// A default set of transforms that adds X-Forwarded-* headers, removes the original Host value and
    /// copies all other request and response fields and headers, except for some protocol specific values.
    /// </summary>
    public static readonly HttpTransformer Default = TransformBuilder.CreateTransformer(new TransformBuilderContext());

    /// <summary>
    /// An empty transformer that copies all request and response fields and headers, except for some
    /// protocol specific values.
    /// </summary>
    public static readonly HttpTransformer Empty = new HttpTransformer();

    /// <summary>
    /// Used to create derived instances.
    /// </summary>
    protected HttpTransformer() { }

    /// <summary>
    /// A callback that is invoked prior to sending the proxied request. All HttpRequestMessage fields are
    /// initialized except RequestUri, which will be initialized after the callback if no value is provided.
    /// See <see cref="RequestUtilities.MakeDestinationAddress(string, PathString, QueryString)"/> for constructing a custom request Uri.
    /// The string parameter represents the destination URI prefix that should be used when constructing the RequestUri.
    /// The headers are copied by the base implementation, excluding some protocol headers like HTTP/2 pseudo headers (":authority").
    /// </summary>
    /// <param name="httpContext">The incoming request.</param>
    /// <param name="proxyRequest">The outgoing proxy request.</param>
    /// <param name="destinationPrefix">The uri prefix for the selected destination server which can be used to create the RequestUri.</param>
    public virtual ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
    {
        foreach (var header in httpContext.Request.Headers)
        {
            var headerName = header.Key;
            var headerValue = header.Value;
            if (RequestUtilities.ShouldSkipRequestHeader(headerName))
            {
                continue;
            }

            RequestUtilities.AddHeader(proxyRequest, headerName, headerValue);
        }

        // https://datatracker.ietf.org/doc/html/rfc7230#section-3.3.3
        // If a message is received with both a Transfer-Encoding and a
        // Content-Length header field, the Transfer-Encoding overrides the
        // Content-Length.  Such a message might indicate an attempt to
        // perform request smuggling (Section 9.5) or response splitting
        // (Section 9.4) and ought to be handled as an error.  A sender MUST
        // remove the received Content-Length field prior to forwarding such
        // a message downstream.
        if (httpContext.Request.Headers.ContainsKey(HeaderNames.TransferEncoding)
            && httpContext.Request.Headers.ContainsKey(HeaderNames.ContentLength))
        {
            proxyRequest.Content?.Headers.Remove(HeaderNames.ContentLength);
        }

        // https://datatracker.ietf.org/doc/html/rfc7540#section-8.1.2.2
        // The only exception to this is the TE header field, which MAY be
        // present in an HTTP/2 request; when it is, it MUST NOT contain any
        // value other than "trailers".
        if (ProtocolHelper.IsHttp2OrGreater(httpContext.Request.Protocol))
        {
            var te = httpContext.Request.Headers.GetCommaSeparatedValues(HeaderNames.TE);
            if (te is not null)
            {
                for (var i = 0; i < te.Length; i++)
                {
                    if (string.Equals(te[i], "trailers", StringComparison.OrdinalIgnoreCase))
                    {
                        var added = proxyRequest.Headers.TryAddWithoutValidation(HeaderNames.TE, te[i]);
                        Debug.Assert(added);
                        break;
                    }
                }
            }
        }

        return default;
    }

    /// <summary>
    /// A callback that is invoked when the proxied response is received. The status code and reason phrase will be copied
    /// to the HttpContext.Response before the callback is invoked, but may still be modified there. The headers will be
    /// copied to HttpContext.Response.Headers by the base implementation, excludes certain protocol headers like
    /// `Transfer-Encoding: chunked`.
    /// </summary>
    /// <param name="httpContext">The incoming request.</param>
    /// <param name="proxyResponse">The response from the destination. This can be null if the destination did not respond.</param>
    /// <returns>A bool indicating if the response should be proxied to the client or not. A derived implementation 
    /// that returns false may send an alternate response inline or return control to the caller for it to retry, respond, 
    /// etc.</returns>
    public virtual ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage? proxyResponse)
    {
        if (proxyResponse is null)
        {
            return new ValueTask<bool>(false);
        }

        var responseHeaders = httpContext.Response.Headers;
        CopyResponseHeaders(proxyResponse.Headers, responseHeaders);
        if (proxyResponse.Content is not null)
        {
            CopyResponseHeaders(proxyResponse.Content.Headers, responseHeaders);
        }

        // https://datatracker.ietf.org/doc/html/rfc7230#section-3.3.3
        // If a message is received with both a Transfer-Encoding and a
        // Content-Length header field, the Transfer-Encoding overrides the
        // Content-Length.  Such a message might indicate an attempt to
        // perform request smuggling (Section 9.5) or response splitting
        // (Section 9.4) and ought to be handled as an error.  A sender MUST
        // remove the received Content-Length field prior to forwarding such
        // a message downstream.
        if (proxyResponse.Content is not null
            && RequestUtilities.ContainsHeader(proxyResponse.Headers, HeaderNames.TransferEncoding)
            && RequestUtilities.ContainsHeader(proxyResponse.Content.Headers, HeaderNames.ContentLength))
        {
            httpContext.Response.Headers.Remove(HeaderNames.ContentLength);
        }

        return new ValueTask<bool>(true);
    }

    /// <summary>
    /// A callback that is invoked after the response body to modify trailers, if supported. The trailers will be
    /// copied to the HttpContext.Response by the base implementation.
    /// </summary>
    /// <param name="httpContext">The incoming request.</param>
    /// <param name="proxyResponse">The response from the destination.</param>
    public virtual ValueTask TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
    {
        // NOTE: Deliberately not using `context.Response.SupportsTrailers()`, `context.Response.AppendTrailer(...)`
        // because they lookup `IHttpResponseTrailersFeature` for every call. Here we do it just once instead.
        var responseTrailersFeature = httpContext.Features.Get<IHttpResponseTrailersFeature>();
        var outgoingTrailers = responseTrailersFeature?.Trailers;
        if (outgoingTrailers is not null && !outgoingTrailers.IsReadOnly)
        {
            // Note that trailers, if any, should already have been declared in Proxy's response
            // by virtue of us having proxied all response headers in step 6.
            CopyResponseHeaders(proxyResponse.TrailingHeaders, outgoingTrailers);
        }

        return default;
    }


    private static void CopyResponseHeaders(HttpHeaders source, IHeaderDictionary destination)
    {
        // We want to append to any prior values, if any.
        // Not using Append here because it skips empty headers.
#if NET6_0_OR_GREATER
        foreach (var header in source.NonValidated)
        {
            var headerName = header.Key;
            if (RequestUtilities.ShouldSkipResponseHeader(headerName))
            {
                continue;
            }

            destination[headerName] = RequestUtilities.Concat(destination[headerName], header.Value);
        }
#else
        foreach (var header in source)
        {
            var headerName = header.Key;
            if (RequestUtilities.ShouldSkipResponseHeader(headerName))
            {
                continue;
            }

            Debug.Assert(header.Value is string[]);
            var values = header.Value as string[] ?? header.Value.ToArray();
            destination[headerName] = StringValues.Concat(destination[headerName], values);
        }
#endif
    }
}
