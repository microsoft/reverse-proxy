// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Service
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
        ValueTask<IProxyConfig> GetConfig();
    }
}
