// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    internal class ProxyErrorFeature : IProxyErrorFeature
    {
        public Exception Error { get; set; }
    }
}
