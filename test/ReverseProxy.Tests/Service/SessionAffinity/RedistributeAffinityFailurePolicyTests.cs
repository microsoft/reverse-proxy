// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Xunit;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public class RedistributeAffinityFailurePolicyTests
    {
        [Theory]
        [InlineData(AffinityStatus.AffinityKeyExtractionFailed)]
        [InlineData(AffinityStatus.DestinationNotFound)]
        public async Task Handle_FailedAffinityStatus_ReturnTrue(AffinityStatus status)
        {
            var policy = new RedistributeAffinityFailurePolicy();

            Assert.Equal(SessionAffinityConstants.AffinityFailurePolicies.Redistribute, policy.Name);
            Assert.True(await policy.Handle(new DefaultHttpContext(), default, status));
        }

        [Theory]
        [InlineData(AffinityStatus.OK)]
        [InlineData(AffinityStatus.AffinityKeyNotSet)]
        public async Task Handle_SuccessfulAffinityStatus_Throw(AffinityStatus status)
        {
            var policy = new RedistributeAffinityFailurePolicy();
            var context = new DefaultHttpContext();

            await Assert.ThrowsAsync<InvalidOperationException>(() => policy.Handle(context, default, status));
        }
    }
}
