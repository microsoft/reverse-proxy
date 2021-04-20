// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;

namespace Yarp.ReverseProxy.Middleware
{
    /// <summary>
    /// An <see cref="IApplicationBuilder"/> for building the `MapReverseProxy` pipeline.
    /// </summary>
    public interface IReverseProxyApplicationBuilder : IApplicationBuilder
    {
    }
}
