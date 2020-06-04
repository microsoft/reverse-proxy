// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Microsoft.ReverseProxy.Signals;
using Moq;

namespace Microsoft.ReverseProxy.Middleware
{
    public abstract class AffinityMiddlewareTestBase
    {
        protected const string AffinitizedDestinationName = "dest-B";
        protected readonly IReadOnlyList<DestinationInfo> Destinations = new[] { new DestinationInfo("dest-A"), new DestinationInfo(AffinitizedDestinationName), new DestinationInfo("dest-C") };
        protected readonly BackendConfig BackendConfig = new BackendConfig(default, default, new BackendConfig.BackendSessionAffinityOptions(true, "Mode-B", "Policy-1", null));

        internal BackendInfo GetBackend()
        {
            var destinationManager = new Mock<IDestinationManager>();
            destinationManager.SetupGet(m => m.Items).Returns(SignalFactory.Default.CreateSignal(Destinations));
            var backend = new BackendInfo("backend-1", destinationManager.Object, new Mock<IProxyHttpClientFactory>().Object);
            backend.Config.Value = BackendConfig;
            return backend;
        }

        internal IReadOnlyList<Mock<ISessionAffinityProvider>> RegisterAffinityProviders(
            bool lookupMiddlewareTest,
            IReadOnlyList<DestinationInfo> expectedDestinations,
            string expectedBackend,
            params (string Mode, AffinityStatus? Status, DestinationInfo[] Destinations, Action<ISessionAffinityProvider> Callback)[] prototypes)
        {
            var result = new List<Mock<ISessionAffinityProvider>>();
            foreach (var (mode, status, destinations, callback) in prototypes)
            {
                var provider = new Mock<ISessionAffinityProvider>(MockBehavior.Strict);
                provider.SetupGet(p => p.Mode).Returns(mode);
                if (lookupMiddlewareTest)
                {
                    provider.Setup(p => p.FindAffinitizedDestinations(
                        It.IsAny<HttpContext>(),
                        expectedDestinations,
                        expectedBackend,
                        BackendConfig.SessionAffinityOptions))
                    .Returns(new AffinityResult(destinations, status.Value))
                    .Callback(() => callback(provider.Object));
                }
                else
                {
                    provider.Setup(p => p.AffinitizeRequest(
                        It.IsAny<HttpContext>(),
                        BackendConfig.SessionAffinityOptions,
                        expectedDestinations[0]))
                    .Callback(() => callback(provider.Object));
                }
                result.Add(provider);
            }
            return result.AsReadOnly();
        }

        internal IReadOnlyList<Mock<IAffinityFailurePolicy>> RegisterFailurePolicies(AffinityStatus expectedStatus, params (string Name, bool Handled, Action<IAffinityFailurePolicy> Callback)[] prototypes)
        {
            var result = new List<Mock<IAffinityFailurePolicy>>();
            foreach (var (name, handled, callback) in prototypes)
            {
                var policy = new Mock<IAffinityFailurePolicy>(MockBehavior.Strict);
                policy.SetupGet(p => p.Name).Returns(name);
                policy.Setup(p => p.Handle(It.IsAny<HttpContext>(), It.Is<BackendConfig.BackendSessionAffinityOptions>(o => o.AffinityFailurePolicy == name), expectedStatus))
                    .ReturnsAsync(handled)
                    .Callback(() => callback(policy.Object));
                result.Add(policy);
            }
            return result.AsReadOnly();
        }

        internal IAvailableDestinationsFeature GetDestinationsFeature(IReadOnlyList<DestinationInfo> destinations)
        {
            var result = new Mock<IAvailableDestinationsFeature>(MockBehavior.Strict);
            result.SetupProperty(p => p.Destinations, destinations);
            return result.Object;
        }
    }
}
