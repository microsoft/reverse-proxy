// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.ReverseProxy.Utilities.Tests
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
            Assert.Equal(Iterations, counter.Value);
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
            Assert.Equal(-Iterations, counter.Value);
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
            Assert.Equal(Max, counter.Value);
            Assert.Equal(Iterations - Max, numCapped);
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
            Assert.Equal(Min, counter.Value);
            Assert.Equal(Min + Iterations, numCapped);
        }
    }
}
