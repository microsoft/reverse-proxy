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
        /// An error that occured when proxying the request ot the destination.
        /// </summary>
        Exception Error { get; set; }
    }
}
