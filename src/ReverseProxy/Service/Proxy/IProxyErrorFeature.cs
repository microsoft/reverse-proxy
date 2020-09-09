// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    public interface IProxyErrorFeature
    {
        Exception Error { get; set; }
    }
}
