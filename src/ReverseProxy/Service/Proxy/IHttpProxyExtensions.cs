// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Service.Proxy
{
    public static class IHttpProxyExtensions
    {
        /// <summary>
        /// Proxies the incoming request to the destination server, and the response back to the client.
        /// </summary>
        /// <param name="context">The HttpContent to proxy from.</param>
        /// <param name="destinationPrefix">The url prefix for where to proxy the request to.</param>
        /// <param name="httpClient">The HTTP client used to send the proxy request.</param>
        public static Task ProxyAsync(
            this IHttpProxy proxy,
            HttpContext context,
            string destinationPrefix,
            HttpMessageInvoker httpClient)
        {
            return proxy.ProxyAsync(context, destinationPrefix, httpClient, requestOptions: default, HttpTransformer.Default);
        }

        /// <summary>
        /// Proxies the incoming request to the destination server, and the response back to the client.
        /// </summary>
        /// <param name="context">The HttpContent to proxy from.</param>
        /// <param name="destinationPrefix">The url prefix for where to proxy the request to.</param>
        /// <param name="httpClient">The HTTP client used to send the proxy request.</param>
        /// <param name="requestOptions">Options for the outgoing request.</param>
        public static Task ProxyAsync(
            this IHttpProxy proxy,
            HttpContext context,
            string destinationPrefix,
            HttpMessageInvoker httpClient,
            RequestProxyOptions requestOptions)
        {
            return proxy.ProxyAsync(context, destinationPrefix, httpClient, requestOptions, HttpTransformer.Default);
        }
    }
}
