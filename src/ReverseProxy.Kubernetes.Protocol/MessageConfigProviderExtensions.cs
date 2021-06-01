// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using System;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Kubernetes.Protocol
{
    public static class MessageConfigProviderExtensions
    {
        public static IReverseProxyBuilder LoadFromMessages(this IReverseProxyBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var provider = new MessageConfigProvider();
            builder.Services.AddSingleton<IProxyConfigProvider>(provider);
            builder.Services.AddSingleton<IUpdateConfig>(provider);
            return builder;
        }
    }
}
