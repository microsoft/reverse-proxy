// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Yarp.ReverseProxy.Configuration.Tests
{
    public class SessionAffinityConfigTests
    {
        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            var options1 = new SessionAffinityConfig
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Provider = "provider1",
                AffinityKeyName = "Key1"
            };

            var options2 = new SessionAffinityConfig
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Provider = "provider1",
                AffinityKeyName = "Key1"
            };

            var equals = options1.Equals(options2);

            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            var options1 = new SessionAffinityConfig
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Provider = "provider1",
                AffinityKeyName = "Key1"
            };

            var options2 = new SessionAffinityConfig
            {
                Enabled = false,
                FailurePolicy = "policy2",
                Provider = "provider2",
                AffinityKeyName = "Key1"
            };

            var equals = options1.Equals(options2);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            var options1 = new SessionAffinityConfig
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Provider = "provider1",
                AffinityKeyName = "Key1"
            };

            var equals = options1.Equals(null);

            Assert.False(equals);
        }
    }
}
