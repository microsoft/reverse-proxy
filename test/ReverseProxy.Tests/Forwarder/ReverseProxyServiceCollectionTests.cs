// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Yarp.Tests.Common;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Microsoft.Extensions.DependencyInjection;

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
