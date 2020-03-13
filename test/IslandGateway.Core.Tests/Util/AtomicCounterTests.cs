// <copyright file="AtomicCounterTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace IslandGateway.Core.Util.Tests
{
    public class AtomicCounterTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new AtomicCounter();
        }

        [Fact]
        public void Increment_ThreadSafety()
        {
            // Arrange
            const int Iterations = 100_000;

            var counter = new AtomicCounter();

            // Act
            Parallel.For(0, Iterations, i =>
            {
                counter.Increment();
            });

            // Assert
            counter.Value.Should().Be(Iterations);
        }

        [Fact]
        public void Decrement_ThreadSafety()
        {
            // Arrange
            const int Iterations = 100_000;

            var counter = new AtomicCounter();

            // Act
            Parallel.For(0, Iterations, i =>
            {
                counter.Decrement();
            });

            // Assert
            counter.Value.Should().Be(-Iterations);
        }

        [Fact]
        public void IncrementCapped_ThreadSafety()
        {
            // Arrange
            const int Iterations = 100_000;
            const int Max = 80_000;

            var counter = new AtomicCounter();
            var numCapped = 0;

            // Act
            Parallel.For(0, Iterations, i =>
            {
                if (!counter.IncrementCapped(Max))
                {
                    Interlocked.Increment(ref numCapped);
                }
            });

            // Assert
            counter.Value.Should().Be(Max);
            numCapped.Should().Be(Iterations - Max);
        }

        [Fact]
        public void DecrementCapped_ThreadSafety()
        {
            // Arrange
            const int Iterations = 100_000;
            const int Min = -80_000;

            var counter = new AtomicCounter();
            var numCapped = 0;

            // Act
            Parallel.For(0, Iterations, i =>
            {
                if (!counter.DecrementCapped(Min))
                {
                    Interlocked.Increment(ref numCapped);
                }
            });

            // Assert
            counter.Value.Should().Be(Min);
            numCapped.Should().Be(Min + Iterations);
        }
    }
}
