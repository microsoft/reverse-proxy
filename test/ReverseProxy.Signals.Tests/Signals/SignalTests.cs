// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.ReverseProxy.Signals.Tests
{
    /// <summary>
    /// Tests for the <see cref="Signal{T}"/> class.
    /// </summary>
    public class SignalTests
    {
        private readonly SignalFactory _factory = new SignalFactory();

        [Fact]
        public void Constructor_Works()
        {
            _factory.CreateSignal<Item>();
        }

        [Fact]
        public void Constructor_WithValue_Works()
        {
            // Act & Assert
            var signal = _factory.CreateSignal(3);
            Assert.Equal(3, signal.Value);
        }

        [Fact]
        public void Constructor_Unit_Works()
        {
            // Act & Assert
            var signal = _factory.CreateUnitSignal();
            Assert.Equal(Unit.Instance, signal.Value);
        }

        [Fact]
        public void Value_Basics()
        {
            // Act & Assert
            var signal = _factory.CreateSignal<Item>();
            Assert.Null(signal.Value);

            var item = new Item();
            signal.Value = item;
            Assert.Equal(item, signal.Value);

            signal.Value = null;
            Assert.Null(signal.Value);
        }

        [Fact]
        public void GetSnapshot_Notifications_Work()
        {
            // Arrange
            var signal = _factory.CreateSignal<Item>();

            // Act & Assert
            var count1 = 0;
            var snapshot1 = signal.GetSnapshot();
            Assert.Null(snapshot1.Value);
            snapshot1.OnChange(() => count1++);
            Assert.Equal(0, count1);

            // Change it once
            var item1 = new Item();
            signal.Value = item1;

            Assert.Equal(1, count1);

            var count1_latesubscription = 0;
            snapshot1.OnChange(() => count1_latesubscription++);
            Assert.Equal(1, count1_latesubscription);

            // Get a new snapshot after we changed the value
            var snapshot2 = signal.GetSnapshot();
            Assert.Equal(item1, snapshot2.Value);

            // Get another snapshot without changing the value
            var snapshot2b = signal.GetSnapshot();
            Assert.Equal(snapshot2, snapshot2b);

            var count2a = 0;
            var count2b = 0;
            snapshot2.OnChange(() => count2a++);
            snapshot2.OnChange(() => count2b++);
            Assert.Equal(0, count2a);
            Assert.Equal(0, count2b);

            // Setting a new value, even if same as old value, should still trigger notifications
            signal.Value = item1;
            Assert.Equal(1, count2a);
            Assert.Equal(1, count2b);

            var snapshot3 = signal.GetSnapshot();
            Assert.NotEqual(snapshot2, snapshot3);
            Assert.Equal(item1, snapshot3.Value);

            // Should not notify previous subscribers again
            Assert.Equal(1, count1);
            Assert.Equal(1, count2a);
            Assert.Equal(1, count2b);
        }

        [Fact]
        public void EndToEndNotifications_ThreadSafety()
        {
            // Arrange
            const int Iterations = 100_000;
            var signal = _factory.CreateSignal<Item>();
            signal.Value = new Item();

            var concurrencyCounter = 0;
            var count = -1;

            // Act & Assert
            signal.Select(item =>
            {
                var concurrency = Interlocked.Increment(ref concurrencyCounter);
                Assert.Equal(1, concurrency);
                Interlocked.Increment(ref count);
                Interlocked.Decrement(ref concurrencyCounter);
                return (object)null;
            });

            Parallel.For(0, Iterations, i =>
            {
                signal.Value = new Item();
            });

            Assert.Equal(Iterations, count);
        }

        private class Item
        {
        }
    }
}
