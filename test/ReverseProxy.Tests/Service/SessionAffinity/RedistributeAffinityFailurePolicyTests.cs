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
        [MemberData(nameof(HandleCases))]
        public async Task Handle_AnyAffinitStatus_ReturnTrue(AffinityStatus status)
        {
            var policy = new RedistributeAffinityFailurePolicy();

            Assert.Equal(SessionAffinityConstants.AffinityFailurePolicies.Redistribute, policy.Name);
            Assert.True(await policy.Handle(new DefaultHttpContext(), default, status));
        }

        public static IEnumerable<object[]> HandleCases()
        {
            // Successful statuses are also included because redistribute policy is not supposed to validate this parameter
            // and therefore must react properly to all affinity statuses.
            foreach(AffinityStatus status in Enum.GetValues(typeof(AffinityStatus)))
            {
                yield return new object[] { status };
            }
        }
    }
}
