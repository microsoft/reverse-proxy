// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using FluentAssertions;
using Xunit;

namespace IslandGateway.Utilities
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
            randomWrapper.Should().NotBeNull();

            // Validate random generation.
            var num = randomWrapper.Next();
            num.Should().BeGreaterOrEqualTo(0);
            num = randomWrapper.Next(5);
            num.Should().BeInRange(0, 5);
            num = randomWrapper.Next(0, 5);
            num.Should().BeInRange(0, 5);
        }
    }
}
