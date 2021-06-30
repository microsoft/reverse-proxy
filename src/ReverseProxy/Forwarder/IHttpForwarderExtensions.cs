// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Forwarder
{
    /// <summary>
    /// Extensions methods for <see cref="IHttpForwarder"/>.
    /// </summary>
    public static class IHttpForwarderExtensions
    {
        /// <summary>
        /// Forwards the incoming request to the destination server, and the response back to the client.
        /// </summary>
        /// <param name="context">The HttpContext to forward.</param>
        /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
        /// <param name="httpClient">The HTTP client used to forward the request.</param>
        /// <returns>The result of forwarding the request and response.</returns>
        public static ValueTask<ForwarderError> SendAsync(this IHttpForwarder forwarder, HttpContext context, string destinationPrefix,
            HttpMessageInvoker httpClient)
        {
            if (forwarder is null)
            {
                throw new ArgumentNullException(nameof(forwarder));
            }

            return forwarder.SendAsync(context, destinationPrefix, httpClient, ForwarderRequestConfig.Empty, HttpTransformer.Default);
        }

        /// <summary>
        /// Forwards the incoming request to the destination server, and the response back to the client.
        /// </summary>
        /// <param name="context">The HttpContext to forward.</param>
        /// <param name="destinationPrefix">The url prefix for where to forward the request to.</param>
        /// <param name="httpClient">The HTTP client used to forward the request.</param>
        /// <param name="requestConfig">Config for the outgoing request.</param>
        /// <returns>The result of forwarding the request and response.</returns>
        public static ValueTask<ForwarderError> SendAsync(this IHttpForwarder forwarder, HttpContext context, string destinationPrefix,
            HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig)
        {
            if (forwarder is null)
            {
                throw new ArgumentNullException(nameof(forwarder));
            }

            return forwarder.SendAsync(context, destinationPrefix, httpClient, requestConfig, HttpTransformer.Default);
        }
    }
}
