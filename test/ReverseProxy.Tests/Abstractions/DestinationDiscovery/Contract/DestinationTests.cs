// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Xunit;

namespace Yarp.ReverseProxy.Abstractions.Tests
{
    public class DestinationTests
    {
        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            var options1 = new Destination
            {
                Address = "https://localhost:10000/destA",
                Health = "https://localhost:20000/destA",
                Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
            };

            var options2 = new Destination
            {
                Address = "https://localhost:10000/destA",
                Health = "https://localhost:20000/destA",
                Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
            };

            var equals = options1.Equals(options2);

            Assert.True(equals);

            Assert.True(options1.Equals(options1 with { })); // Clone
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {

            var options1 = new Destination
            {
                Address = "https://localhost:10000/destA",
                Health = "https://localhost:20000/destA",
                Metadata = new Dictionary<string, string> { { "destA-K1", "destA-V1" }, { "destA-K2", "destA-V2" } }
            };

            Assert.False(options1.Equals(options1 with { Address = "different" }));
            Assert.False(options1.Equals(options1 with { Health = null }));
            Assert.False(options1.Equals(options1 with
            {
                Metadata = new Dictionary<string, string>
                {
                    { "destB-K1", "destB-V1" }, { "destB-K2", "destB-V2" }
                }
            }));
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            var options1 = new Destination();

            var equals = options1.Equals(null);

            Assert.False(equals);
        }
    }
}
