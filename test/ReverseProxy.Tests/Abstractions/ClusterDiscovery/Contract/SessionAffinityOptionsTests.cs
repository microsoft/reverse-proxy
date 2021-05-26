// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Yarp.ReverseProxy.Abstractions.ClusterDiscovery.Contract
{
    public class SessionAffinityOptionsTests
    {
        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            var options1 = new SessionAffinityConfig("Key1")
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Mode = "mode1"
            };

            var options2 = new SessionAffinityConfig("Key1")
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Mode = "mode1"
            };

            var equals = options1.Equals(options2);

            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            var options1 = new SessionAffinityConfig("Key1")
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Mode = "mode1"
            };

            var options2 = new SessionAffinityConfig("Key1")
            {
                Enabled = false,
                FailurePolicy = "policy2",
                Mode = "mode2"
            };

            var equals = options1.Equals(options2);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            var options1 = new SessionAffinityConfig("Key1")
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Mode = "mode1"
            };

            var equals = options1.Equals(null);

            Assert.False(equals);
        }
    }
}
