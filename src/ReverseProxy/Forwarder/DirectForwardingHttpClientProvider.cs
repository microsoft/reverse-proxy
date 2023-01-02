// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Forwarder;

internal class DirectForwardingHttpClientProvider
{
    public HttpMessageInvoker HttpClient { get; }

    public DirectForwardingHttpClientProvider() : this(new ForwarderHttpClientFactory()) { }

    public DirectForwardingHttpClientProvider(IForwarderHttpClientFactory factory)
    {
        HttpClient = factory.CreateClient(new ForwarderHttpClientContext
        {
            NewConfig = HttpClientConfig.Empty
        });
    }
}
