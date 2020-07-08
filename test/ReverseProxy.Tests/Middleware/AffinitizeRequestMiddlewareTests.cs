// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Middleware
{
    public class AffinitizeRequestMiddlewareTests : AffinityMiddlewareTestBase
    {
        [Fact]
        public async Task Invoke_SingleDestinationChosen_InvokeAffinitizeRequest()
        {
            var cluster = GetCluster();
            var invokedMode = string.Empty;
            const string expectedMode = "Mode-B";
            var providers = RegisterAffinityProviders(
                false,
                Destinations[1],
                cluster.ClusterId,
                ("Mode-A", (AffinityStatus?)null, (DestinationInfo[])null, (Action<ISessionAffinityProvider>)(p => throw new InvalidOperationException($"Provider {p.Mode} call is not expected."))),
                (expectedMode, (AffinityStatus?)null, (DestinationInfo[])null, (Action<ISessionAffinityProvider>)(p => invokedMode = p.Mode)));
            var nextInvoked = false;
            var middleware = new AffinitizeRequestMiddleware(c => {
                    nextInvoked = true;
                    return Task.CompletedTask;
                },
                providers.Select(p => p.Object),
                new Mock<ILogger<AffinitizeRequestMiddleware>>().Object);
            var context = new DefaultHttpContext();
            context.Features.Set(cluster);
            var destinationFeature = GetDestinationsFeature(Destinations[1], cluster.Config.Value);
            context.Features.Set(destinationFeature);

            await middleware.Invoke(context);

            Assert.Equal(expectedMode, invokedMode);
            Assert.True(nextInvoked);
            providers[0].VerifyGet(p => p.Mode, Times.Once);
            providers[0].VerifyNoOtherCalls();
            providers[1].VerifyAll();
            Assert.Same(destinationFeature.AvailableDestinations, Destinations[1]);
        }

        [Fact]
        public async Task Invoke_MultipleCandidateDestinations_ChooseOneAndInvokeAffinitizeRequest()
        {
            var cluster = GetCluster();
            var invokedMode = string.Empty;
            const string expectedMode = "Mode-B";
            var providers = new[] {
                GetProviderForRandomDestination("Mode-A", Destinations, p => throw new InvalidOperationException($"Provider {p.Mode} call is not expected.")),
                GetProviderForRandomDestination(expectedMode, Destinations, p => invokedMode = p.Mode)
            };
            var nextInvoked = false;
            var logger = AffinityTestHelper.GetLogger<AffinitizeRequestMiddleware>();
            var middleware = new AffinitizeRequestMiddleware(c => {
                nextInvoked = true;
                return Task.CompletedTask;
            },
                providers.Select(p => p.Object),
                logger.Object);
            var context = new DefaultHttpContext();
            context.Features.Set(cluster);
            var destinationFeature = GetDestinationsFeature(Destinations, cluster.Config.Value);
            context.Features.Set(destinationFeature);

            await middleware.Invoke(context);

            Assert.Equal(expectedMode, invokedMode);
            Assert.True(nextInvoked);
            providers[0].VerifyGet(p => p.Mode, Times.Once);
            providers[0].VerifyNoOtherCalls();
            providers[1].VerifyAll();
            logger.Verify(
                l => l.Log(LogLevel.Warning, EventIds.MultipleDestinationsOnClusterToEstablishRequestAffinity, It.IsAny<It.IsAnyType>(), null, (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
            Assert.Equal(1, destinationFeature.AvailableDestinations.Count);
            var chosen = destinationFeature.AvailableDestinations[0];
            var sameDestinationCount = Destinations.Count(d => chosen == d);
            Assert.Equal(1, sameDestinationCount);
        }

        [Fact]
        public async Task Invoke_NoDestinationChosen_LogWarningAndCallNext()
        {
            var cluster = GetCluster();
            var nextInvoked = false;
            var logger = AffinityTestHelper.GetLogger<AffinitizeRequestMiddleware>();
            var middleware = new AffinitizeRequestMiddleware(c => {
                    nextInvoked = true;
                    return Task.CompletedTask;
                },
                new ISessionAffinityProvider[0],
                logger.Object);
            var context = new DefaultHttpContext();
            context.Features.Set(cluster);
            var destinationFeature = GetDestinationsFeature(new DestinationInfo[0], cluster.Config.Value);
            context.Features.Set(destinationFeature);

            await middleware.Invoke(context);

            Assert.True(nextInvoked);
            logger.Verify(
                l => l.Log(LogLevel.Warning, EventIds.NoDestinationOnClusterToEstablishRequestAffinity, It.IsAny<It.IsAnyType>(), null, (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
            Assert.Equal(0, destinationFeature.AvailableDestinations.Count);
        }

        private Mock<ISessionAffinityProvider> GetProviderForRandomDestination(string mode, IReadOnlyList<DestinationInfo> destinations, Action<ISessionAffinityProvider> callback)
        {
            var provider = new Mock<ISessionAffinityProvider>(MockBehavior.Strict);
            provider.SetupGet(p => p.Mode).Returns(mode);
            provider.Setup(p => p.AffinitizeRequest(It.IsAny<HttpContext>(), ClusterConfig.SessionAffinityOptions, It.Is<DestinationInfo>(d => destinations.Contains(d))))
                .Callback(() => callback(provider.Object));
            return provider;
        }
    }
}
