// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Microsoft.ReverseProxy.Utilities
{
    public class RandomWrapperTests
    {
        [Fact]
        public void RandomWrapper_Work()
        {
            // Set up the random instance.
            var random = new Random();
            var randomWrapper = new RandomWrapper(random);

            // Validate.
            Assert.NotNull(randomWrapper);

            // Validate random generation.
            var num = randomWrapper.Next();
            Assert.True(num >= 0);
            num = randomWrapper.Next(5);
            Assert.InRange(num, 0, 5);
            num = randomWrapper.Next(0, 5);
            Assert.InRange(num, 0, 5);
        }
    }
}
