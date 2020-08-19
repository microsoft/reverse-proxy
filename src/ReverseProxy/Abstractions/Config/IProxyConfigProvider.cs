// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service
{
    /// <summary>
    /// A data source for proxy route and cluster information.
    /// </summary>
    public interface IProxyConfigProvider
    {
        /// <summary>
        /// Returns the current route and cluster data.
        /// </summary>
        /// <returns></returns>
        IProxyConfig GetConfig();
    }
}
