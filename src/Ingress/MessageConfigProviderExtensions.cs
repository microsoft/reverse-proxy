// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Configuration;
using Microsoft.ReverseProxy.Service;
using Ingress;
using Ingress.Services;

namespace Microsoft.Extensions.DependencyInjection
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
