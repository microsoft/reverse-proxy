// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Microsoft.ReverseProxy.Signals;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Middleware
{
    public class AffinitizedDestinationLookupMiddlewareTests : AffinityMiddlewareTestBase
    {
        [Theory]
        [InlineData(AffinityStatus.AffinityKeyNotSet, null)]
        [InlineData(AffinityStatus.OK, AffinitizedDestinationName)]
        public async Task Invoke_SuccessfulFlow_CallNext(AffinityStatus status, string foundDestinationId)
        {
            var backend = GetBackend();
            var foundDestinations = foundDestinationId != null ? Destinations.Where(d => d.DestinationId == foundDestinationId).ToArray() : null;
            var invokedMode = string.Empty;
            const string expectedMode = "Mode-B";
            var providers = RegisterAffinityProviders(
                true,
                Destinations,
                backend.BackendId,
                ("Mode-A", AffinityStatus.DestinationNotFound, (RuntimeModel.DestinationInfo[])null, (Action<ISessionAffinityProvider>)(p => throw new InvalidOperationException($"Provider {p.Mode} call is not expected."))),
                (expectedMode, status, foundDestinations, p => invokedMode = p.Mode));
            var nextInvoked = false;
            var middleware = new AffinitizedDestinationLookupMiddleware(c => {
                    nextInvoked = true;
                    return Task.CompletedTask;
                },
                providers.Select(p => p.Object), new IAffinityFailurePolicy[0],
                GetOperationLogger(false),
                new Mock<ILogger<AffinitizedDestinationLookupMiddleware>>().Object);
            var context = new DefaultHttpContext();
            context.Features.Set(backend);
            var destinationFeature = GetDestinationsFeature(Destinations);
            context.Features.Set(destinationFeature);

            await middleware.Invoke(context);

            Assert.Equal(expectedMode, invokedMode);
            Assert.True(nextInvoked);
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
                Assert.Same(Destinations, destinationFeature.Destinations);
            }
        }

        [Theory]
        [InlineData(AffinityStatus.DestinationNotFound, true)]
        [InlineData(AffinityStatus.DestinationNotFound, false)]
        [InlineData(AffinityStatus.AffinityKeyExtractionFailed, true)]
        [InlineData(AffinityStatus.AffinityKeyExtractionFailed, false)]
        public async Task Invoke_ErrorFlow_CallFailurePolicy(AffinityStatus affinityStatus, bool keepProcessing)
        {
            var backend = GetBackend();
            var providers = RegisterAffinityProviders(true, Destinations, backend.BackendId, ("Mode-B", affinityStatus, null, _ => { }));
            var invokedPolicy = string.Empty;
            const string expectedPolicy = "Policy-1";
            var failurePolicies = RegisterFailurePolicies(
                affinityStatus,
                ("Policy-0", false, p => throw new InvalidOperationException($"Policy {p.Name} call is not expected.")),
                (expectedPolicy, keepProcessing, p => invokedPolicy = p.Name));
            var nextInvoked = false;
            var logger = AffinityTestHelper.GetLogger<AffinitizedDestinationLookupMiddleware>();
            var middleware = new AffinitizedDestinationLookupMiddleware(c => {
                    nextInvoked = true;
                    return Task.CompletedTask;
                },
                providers.Select(p => p.Object), failurePolicies.Select(p => p.Object),
                GetOperationLogger(true),
                logger.Object);
            var context = new DefaultHttpContext();
            context.Features.Set(backend);
            var destinationFeature = GetDestinationsFeature(Destinations);
            context.Features.Set(destinationFeature);

            await middleware.Invoke(context);

            Assert.Equal(expectedPolicy, invokedPolicy);
            Assert.Equal(keepProcessing, nextInvoked);
            failurePolicies[0].VerifyGet(p => p.Name, Times.Once);
            failurePolicies[0].VerifyNoOtherCalls();
            failurePolicies[1].VerifyAll();
            if (!keepProcessing)
            {
                logger.Verify(
                    l => l.Log(LogLevel.Warning, EventIds.AffinityResolutionFailedForBackend, It.IsAny<It.IsAnyType>(), null, (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                    Times.Once);
            }
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
