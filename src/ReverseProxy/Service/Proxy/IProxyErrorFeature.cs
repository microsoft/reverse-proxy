// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Stores errors that occurred when proxying the request to the destination.
    /// </summary>
    public interface IProxyErrorFeature
    {
        /// <summary>
        /// The specified ProxyErrorCode.
        /// </summary>
        public ProxyErrorCode ErrorCode { get; }

        /// <summary>
        /// An error that occurred when proxying the request to the destination.
        /// </summary>
        public Exception Error { get; }
    }
}
