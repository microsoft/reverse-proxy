// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using System;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Service;

namespace Yarp.ReverseProxy.WebApp
{
    public static class MessageConfigProviderExtensions
    {
        public static IReverseProxyBuilder LoadFromMessages(this IReverseProxyBuilder builder)
        {
            var provider = new MessageConfigProvider();
            builder.Services.AddSingleton<IProxyConfigProvider>(provider);
            builder.Services.AddSingleton<IUpdateConfig>(provider);
            return builder;
        }
    }
}
