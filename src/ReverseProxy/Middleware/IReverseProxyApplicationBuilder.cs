// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Middleware
{
    public interface IReverseProxyApplicationBuilder : IApplicationBuilder
    {
    }
}
