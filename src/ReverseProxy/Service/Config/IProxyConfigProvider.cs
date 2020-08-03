// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service
{
    public interface IProxyConfigProvider
    {
        IProxyConfig GetConfig();
    }
}
