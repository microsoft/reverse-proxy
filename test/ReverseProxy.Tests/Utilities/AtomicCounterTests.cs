// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    }
}
