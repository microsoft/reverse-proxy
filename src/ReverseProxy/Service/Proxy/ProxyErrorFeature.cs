// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    public class ProxyErrorFeature : IProxyErrorFeature
    {
        public Exception Error { get; set; }
    }
}
