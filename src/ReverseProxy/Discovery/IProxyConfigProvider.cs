// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Discovery
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
