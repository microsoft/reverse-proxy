// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Yarp.ReverseProxy.Utilities
{
    public class RandomFactoryTests
    {
        [Fact]
        public void RandomFactory_Work()
        {
            // Set up the factory.
            var factory = new RandomFactory();

            // Create random class object.
            var random = factory.CreateRandomInstance();

            // Validate.
            Assert.NotNull(random);
            Assert.IsType<Random>(random);

            // Validate functionality
            var num = random.Next(5);
            Assert.InRange(num, 0, 5);
        }
    }
}
