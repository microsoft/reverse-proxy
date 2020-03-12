// <copyright file="SignalTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace IslandGateway.Signals.Tests
{
    /// <summary>
    /// Tests for the <see cref="Signal{T}"/> class.
    /// </summary>
    public class SignalTests
    {
        private SignalFactory factory = new SignalFactory();

        [Fact]
        public void Constructor_Works()
        {
            this.factory.CreateSignal<Item>();
        }

        [Fact]
        public void Constructor_WithValue_Works()
        {
            // Act & Assert
            Signal<int> signal = this.factory.CreateSignal(3);
            signal.Value.Should().Be(3);
        }

        [Fact]
        public void Constructor_Unit_Works()
        {
            // Act & Assert
            Signal<Unit> signal = this.factory.CreateUnitSignal();
            signal.Value.Should().BeSameAs(Unit.Instance);
        }

        [Fact]
        public void Value_Basics()
        {
            // Act & Assert
            var signal = this.factory.CreateSignal<Item>();
            signal.Value.Should().BeNull();

            var item = new Item();
            signal.Value = item;
            signal.Value.Should().BeSameAs(item);

            signal.Value = null;
            signal.Value.Should().BeNull();
        }

        [Fact]
        public void GetSnapshot_Notifications_Work()
        {
            // Arrange
            var signal = this.factory.CreateSignal<Item>();

            // Act & Assert
            int count1 = 0;
            var snapshot1 = signal.GetSnapshot();
            snapshot1.Value.Should().BeNull();
            snapshot1.OnChange(() => count1++);
            count1.Should().Be(0);

            // Change it once
            var item1 = new Item();
            signal.Value = item1;

            count1.Should().Be(1);

            int count1_latesubscription = 0;
            snapshot1.OnChange(() => count1_latesubscription++);
            count1_latesubscription.Should().Be(1);

            // Get a new snapshot after we changed the value
            var snapshot2 = signal.GetSnapshot();
            snapshot2.Value.Should().BeSameAs(item1);

            // Get another snapshot without changing the value
            var snapshot2b = signal.GetSnapshot();
            snapshot2b.Should().BeSameAs(snapshot2);

            int count2a = 0;
            int count2b = 0;
            snapshot2.OnChange(() => count2a++);
            snapshot2.OnChange(() => count2b++);
            count2a.Should().Be(0);
            count2b.Should().Be(0);

            // Setting a new value, even if same as old value, should still trigger notifications
            signal.Value = item1;
            count2a.Should().Be(1);
            count2b.Should().Be(1);

            var snapshot3 = signal.GetSnapshot();
            snapshot3.Should().NotBeSameAs(snapshot2);
            snapshot3.Value.Should().BeSameAs(item1);

            // Should not notify previous subscribers again
            count1.Should().Be(1);
            count2a.Should().Be(1);
            count2b.Should().Be(1);
        }

        [Fact]
        public void EndToEndNotifications_ThreadSafety()
        {
            // Arrange
            const int Iterations = 100_000;
            var signal = this.factory.CreateSignal<Item>();
            signal.Value = new Item();

            int concurrencyCounter = 0;
            int count = -1;

            // Act & Assert
            signal.Select(item =>
            {
                int concurrency = Interlocked.Increment(ref concurrencyCounter);
                concurrency.Should().Be(1);
                Interlocked.Increment(ref count);
                Interlocked.Decrement(ref concurrencyCounter);
                return (object)null;
            });

            Parallel.For(0, Iterations, i =>
            {
                signal.Value = new Item();
            });

            count.Should().Be(Iterations);
        }

        private class Item
        {
        }
    }
}
