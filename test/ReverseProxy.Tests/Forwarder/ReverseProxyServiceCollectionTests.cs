// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Yarp.ReverseProxy.Forwarder;

public class ReverseProxyServiceCollectionTests
{

    [Fact]
    public void ConfigureHttpClient_Works()
    {
        new ServiceCollection()
            .AddReverseProxy()
            .ConfigureHttpClient((_, _) => { });
    }

    [Fact] 
    public void ConfigureHttpClient_ThrowIfCustomServiceAdded()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            new ServiceCollection()
                .AddSingleton<IForwarderHttpClientFactory, CustomForwarderHttpClientFactory>()
                .AddReverseProxy()
                .ConfigureHttpClient((_, _) => { });
        });
    }

    private class CustomForwarderHttpClientFactory : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
        {
            throw new NotImplementedException();
        }
    }
}
