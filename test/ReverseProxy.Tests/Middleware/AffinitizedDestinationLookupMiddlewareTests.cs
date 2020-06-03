// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Microsoft.ReverseProxy.Signals;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Middleware
{
    public class AffinitizedDestinationLookupMiddlewareTests
    {
        private const string AffinitizedDestinationName = "dest-B";
        private readonly IReadOnlyList<DestinationInfo> _destinations = new[] { new DestinationInfo("dest-A"), new DestinationInfo(AffinitizedDestinationName), new DestinationInfo("dest-C") };
        private readonly BackendConfig _backendConfig = new BackendConfig(default, default, new BackendConfig.BackendSessionAffinityOptions(true, "Mode-B", "Policy-1", null));

        [Theory]
        [InlineData(AffinityStatus.AffinityKeyNotSet, null)]
        [InlineData(AffinityStatus.AffinityDisabled, null)]
        [InlineData(AffinityStatus.OK, AffinitizedDestinationName)]
        public async Task Invoke_SuccessfulFlow_CallNext(AffinityStatus status, string foundDestinationId)
        {
            var backend = GetBackend();
            var foundDestinations = foundDestinationId != null ? _destinations.Where(d => d.DestinationId == foundDestinationId).ToArray() : null;
            var invokedMode = string.Empty;
            var providers = RegisterAffinityProviders(
                _destinations,
                backend.BackendId,
                ("Mode-A", AffinityStatus.DestinationNotFound, null, p => throw new InvalidOperationException($"Provider {p.Mode} call is not expected.")),
                ("Mode-B", status, foundDestinations, p => invokedMode = p.Mode));
            var middleware = new AffinitizedDestinationLookupMiddleware(c => Task.CompletedTask,
                providers.Select(p => p.Object), new IAffinityFailurePolicy[0],
                GetOperationLogger(false),
                new Mock<ILogger<AffinitizedDestinationLookupMiddleware>>().Object);
            var context = new DefaultHttpContext();
            context.Features.Set(backend);
            var destinationFeature = GetDestinationsFeature(_destinations);
            context.Features.Set(destinationFeature);

            await middleware.Invoke(context);

            Assert.Equal("Mode-B", invokedMode);
            providers[0].VerifyGet(p => p.Mode, Times.Once);
            providers[0].VerifyNoOtherCalls();
            providers[1].VerifyAll();

            if (foundDestinationId != null)
            {
                Assert.Equal(1, destinationFeature.Destinations.Count);
                Assert.Equal(foundDestinationId, destinationFeature.Destinations[0].DestinationId);
            }
            else
            {
                Assert.Same(_destinations, destinationFeature.Destinations);
            }
        }

        [Theory]
        [InlineData(AffinityStatus.DestinationNotFound, true)]
        [InlineData(AffinityStatus.DestinationNotFound, false)]
        [InlineData(AffinityStatus.AffinityKeyExtractionFailed, true)]
        [InlineData(AffinityStatus.AffinityKeyExtractionFailed, false)]
        public async Task Invoke_ErrorFlow_CallFailurePolicy(AffinityStatus affinityStatus, bool handled)
        {
            var backend = GetBackend();
            var providers = RegisterAffinityProviders(_destinations, backend.BackendId, ("Mode-B", affinityStatus, null, _ => { }));
            var invokedPolicy = string.Empty;
            var failurePolicies = RegisterFailurePolicies(
                affinityStatus,
                ("Policy-0", false, p => throw new InvalidOperationException($"Policy {p.Name} call is not expected.")),
                ("Policy-1", handled, p => invokedPolicy = p.Name));
            var nextInvoked = false;
            var middleware = new AffinitizedDestinationLookupMiddleware(c => {
                    nextInvoked = true;
                    return Task.CompletedTask;
                },
                providers.Select(p => p.Object), failurePolicies.Select(p => p.Object),
                GetOperationLogger(true),
                new Mock<ILogger<AffinitizedDestinationLookupMiddleware>>().Object);
            var context = new DefaultHttpContext();
            context.Features.Set(backend);
            var destinationFeature = GetDestinationsFeature(_destinations);
            context.Features.Set(destinationFeature);

            await middleware.Invoke(context);

            Assert.Equal("Policy-1", invokedPolicy);
            Assert.Equal(handled, nextInvoked);
            failurePolicies[0].VerifyGet(p => p.Name, Times.Once);
            failurePolicies[0].VerifyNoOtherCalls();
            failurePolicies[1].VerifyAll();
        }

        private BackendInfo GetBackend()
        {
            var destinationManager = new Mock<IDestinationManager>();
            destinationManager.SetupGet(m => m.Items).Returns(SignalFactory.Default.CreateSignal(_destinations));
            var backend = new BackendInfo("backend-1", destinationManager.Object, new Mock<IProxyHttpClientFactory>().Object);
            backend.Config.Value = _backendConfig;
            return backend;
        }

        private IReadOnlyList<Mock<ISessionAffinityProvider>> RegisterAffinityProviders(
            IReadOnlyList<DestinationInfo> expectedDestinations,
            string expectedBackend,
            params (string Mode, AffinityStatus Status, DestinationInfo[] Destinations, Action<ISessionAffinityProvider> Callback)[] prototypes)
        {
            var result = new List<Mock<ISessionAffinityProvider>>();
            foreach (var (mode, status, destinations, callback) in prototypes)
            {
                var provider = new Mock<ISessionAffinityProvider>(MockBehavior.Strict);
                provider.SetupGet(p => p.Mode).Returns(mode);
                provider.Setup(p => p.FindAffinitizedDestinations(
                    It.IsAny<HttpContext>(),
                    expectedDestinations,
                    expectedBackend,
                    _backendConfig.SessionAffinityOptions))
                    .Returns(new AffinityResult(destinations, status))
                    .Callback(() => callback(provider.Object));
                result.Add(provider);
            }
            return result.AsReadOnly();
        }

        private IReadOnlyList<Mock<IAffinityFailurePolicy>> RegisterFailurePolicies(AffinityStatus expectedStatus, params (string Name, bool Handled, Action<IAffinityFailurePolicy> Callback)[] prototypes)
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

        private IAvailableDestinationsFeature GetDestinationsFeature(IReadOnlyList<DestinationInfo> destinations)
        {
            var result = new Mock<IAvailableDestinationsFeature>(MockBehavior.Strict);
            result.SetupProperty(p => p.Destinations, destinations);
            return result.Object;
        }

        private IOperationLogger<AffinitizedDestinationLookupMiddleware> GetOperationLogger(bool callFailurePolicy)
        {
            var result = new Mock<IOperationLogger<AffinitizedDestinationLookupMiddleware>>(MockBehavior.Strict);
            result.Setup(l => l.Execute(It.IsAny<string>(), It.IsAny<Func<AffinityResult>>())).Returns((string name, Func<AffinityResult> callback) => callback());
            if (callFailurePolicy)
            {
                result.Setup(l => l.ExecuteAsync(It.IsAny<string>(), It.IsAny<Func<Task<bool>>>())).Returns(async (string name, Func<Task<bool>> callback) => await callback());
            }
            return result.Object;
        }
    }
}
