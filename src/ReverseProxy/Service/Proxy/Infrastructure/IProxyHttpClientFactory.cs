// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.Service.Proxy.Infrastructure
{
    /// <summary>
    /// Provides a method to create instances of <see cref="HttpMessageInvoker"/>
    /// for proxying requests to an upstream server.
    /// </summary>
    /// <remarks>
    /// This is somewhat similarly to `System.Net.Http.IHttpClientFactory`,
    /// except that this factory class is meant for direct use,
    /// which Proxy requires in order to keep separate pools for each cluster.
    /// </remarks>
    public interface IProxyHttpClientFactory
    {
        /// <summary>
        /// Creates and configures an <see cref="HttpMessageInvoker"/> instance
        /// that can be used for proxying requests to an upstream server.
        /// </summary>
        /// <param name="context">An <see cref="ProxyHttpClientContext"/> carrying old and new cluster configurations.</param>
        /// <remarks>
        /// <para>
        /// Each call to <see cref="CreateClient(ProxyHttpClientContext)"/> is guaranteed
        /// to return a new <see cref="HttpMessageInvoker"/> instance.
        /// It is generally not necessary to dispose of the <see cref="HttpMessageInvoker"/>
        /// as the <see cref="IProxyHttpClientFactory"/> tracks and disposes resources
        /// used by the <see cref="HttpMessageInvoker"/>.
        /// </para>
        /// </remarks>
        HttpMessageInvoker CreateClient(ProxyHttpClientContext context);
    }
}
