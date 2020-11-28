// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Stores errors and exceptions that occurred when proxying the request to the destination.
    /// </summary>
    public interface IProxyErrorFeature
    {
        /// <summary>
        /// The specified ProxyError.
        /// </summary>
        public ProxyError Error { get; }

        /// <summary>
        /// An Exception that occurred when proxying the request to the destination.
        /// </summary>
        public Exception Exception { get; }
    }
}
