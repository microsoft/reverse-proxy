// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.Sample
{
    public static class HttpForwarderExtensions
    {
        /// <summary>
        /// Forwards the incoming request to the destination server, and the response back to the client.
        /// </summary>
        /// <param name="context">The HttpContext to forward.</param>
        /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
        /// <param name="httpClient">The HTTP client used to forward the request.</param>
        /// <param name="requestTransform">Transform function to apply to the forwarded request.</param>
        /// <returns>The status of a forwarding operation.</returns>
        public static ValueTask<ForwarderError> SendAsync(this IHttpForwarder forwarder, HttpContext context, string destinationPrefix,
            HttpMessageInvoker httpClient, Func<HttpContext, HttpRequestMessage, ValueTask> requestTransform)
        {
            return forwarder.SendAsync(context, destinationPrefix, httpClient, ForwarderRequestConfig.Empty, requestTransform);
        }

        /// <summary>
        /// Forwards the incoming request to the destination server, and the response back to the client.
        /// </summary>
        /// <param name="context">The HttpContext to forward.</param>
        /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
        /// <param name="httpClient">The HTTP client used to forward the request.</param>
        /// <param name="requestConfig">Config for the outgoing request.</param>
        /// <param name="requestTransform">Transform function to apply to the forwarded request.</param>
        /// <returns>The status of a forwarding operation.</returns>
        public static ValueTask<ForwarderError> SendAsync(this IHttpForwarder forwarder, HttpContext context, string destinationPrefix,
            HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig, Func<HttpContext, HttpRequestMessage, ValueTask> requestTransform)
        {
            return forwarder.SendAsync(context, destinationPrefix, httpClient, requestConfig, new RequestTransformer(requestTransform));
        }
    }
}
