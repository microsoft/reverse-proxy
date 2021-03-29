// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Service;

namespace Yarp.ReverseProxy.Kubernetes.Protocol
{
    public class MessageConfigProvider : IProxyConfigProvider, IUpdateConfig
    {
        private volatile MessageConfig _config;

        public MessageConfigProvider()
        {
            _config = new MessageConfig(null, null);
        }

        public IProxyConfig GetConfig() => _config;

        public void Update(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters)
        {
            var oldConfig = _config;
            _config = new MessageConfig(routes, clusters);
            oldConfig.SignalChange();
        }

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
        private class MessageConfig : IProxyConfig
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
        {
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();

            public MessageConfig(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters)
            {
                Routes = routes;
                Clusters = clusters;
                ChangeToken = new CancellationChangeToken(_cts.Token);
            }

            public IReadOnlyList<ProxyRoute> Routes { get; }

            public IReadOnlyList<Cluster> Clusters { get; }

            public IChangeToken ChangeToken { get; }

            internal void SignalChange()
            {
                _cts.Cancel();
            }
        }
    }
}
