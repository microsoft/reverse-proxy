// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Xunit;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public class Return503ErrorAffinityFailurePolicyTests
    {
        [Theory]
        [InlineData(AffinityStatus.DestinationNotFound)]
        [InlineData(AffinityStatus.AffinityKeyExtractionFailed)]
        public async Task Handle_FaultyAffinityStatus_RespondWith503(AffinityStatus status)
        {
            var policy = new Return503ErrorAffinityFailurePolicy();
            var context = new DefaultHttpContext();

            Assert.Equal(SessionAffinityConstants.AffinityFailurePolicies.Return503Error, policy.Name);

            Assert.False(await policy.Handle(context, default, status));
            Assert.Equal(503, context.Response.StatusCode);
        }

        [Theory]
        [InlineData(AffinityStatus.OK)]
        [InlineData(AffinityStatus.AffinityKeyNotSet)]
        [InlineData(AffinityStatus.AffinityDisabled)]
        public async Task Handle_SuccessfulAffinityStatus_ReturnTrue(AffinityStatus status)
        {
            var policy = new Return503ErrorAffinityFailurePolicy();
            var context = new DefaultHttpContext();

            Assert.True(await policy.Handle(context, default, status));
            Assert.Equal(200, context.Response.StatusCode);
        }
    }
}
