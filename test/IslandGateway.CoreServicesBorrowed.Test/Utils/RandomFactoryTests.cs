// <copyright file="RandomFactoryTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using FluentAssertions;
using IslandGateway.CoreServicesBorrowed;
using Xunit;

namespace IslandGateway.CoreServicesBorrowed
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
            random.Should().NotBeNull();
            random.GetType().Should().Be(typeof(RandomWrapper));

            // Validate functionality
            int num = random.Next(5);
            num.Should().BeInRange(0, 5);
        }
    }
}
