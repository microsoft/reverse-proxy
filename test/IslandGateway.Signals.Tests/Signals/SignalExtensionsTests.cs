// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Microsoft.ReverseProxy.Signals.Tests
{
    /// <summary>
    /// Tests for the <see cref="Signal{T}"/> class.
    /// </summary>
    public class SignalExtensionsTests
    {
        [Fact]
        public void Select_Works()
        {
            // Arrange
            var signal = SignalFactory.Default.CreateSignal<Item>();
            var derived = signal.Select(item => item?.Id ?? -1);

            // Act & Assert
            derived.Value.Should().Be(-1);

            signal.Value = new Item(1);
            derived.Value.Should().Be(1);

            signal.Value = new Item(7);
            derived.Value.Should().Be(7);

            signal.Value = null;
            derived.Value.Should().Be(-1);
        }

        [Fact]
        public void Flatten_Works()
        {
            // Arrange
            var a = SignalFactory.Default.CreateSignal<int>(0);
            var b = SignalFactory.Default.CreateSignal<int>(2);

            var selector = SignalFactory.Default.CreateSignal<IReadableSignal<int>>(a);

            // Act
            var x = selector.Flatten();

            x.Value.Should().Be(0);

            a.Value = 1;
            x.Value.Should().Be(1);

            selector.Value = b;
            x.Value.Should().Be(2);
            var notified = false;
            x.GetSnapshot().OnChange(() => notified = true);

            a.Value = 3;
            x.Value.Should().Be(2);
            notified.Should().BeFalse();

            selector.Value = null;
            x.Value.Should().Be(default);
        }

        [Fact]
        public void Flatten_MismatchedContext_Throws()
        {
            // Arrange
            var signal1 = new SignalFactory().CreateSignal<int>();
            var signal2 = new SignalFactory().CreateSignal(signal1);

            // Act
            Action action = () => signal2.Flatten();

            // Assert
            action.Should().ThrowExactly<InvalidOperationException>()
                .WithMessage("*Cannot mix signals*");
        }

        [Fact]
        public void SelectMany_Works()
        {
            // Arrange
            var a = SignalFactory.Default.CreateSignal<int>(1);
            var b = SignalFactory.Default.CreateSignal<int>(2);
            var x = a.SelectMany(a_ => b.Select(b_ => (aVal: a_, bVal: b_)));

            // Act & Assert
            x.Value.aVal.Should().Be(1);
            x.Value.bVal.Should().Be(2);

            a.Value = 3;
            x.Value.aVal.Should().Be(3);
            x.Value.bVal.Should().Be(2);

            b.Value = 4;
            x.Value.aVal.Should().Be(3);
            x.Value.bVal.Should().Be(4);
        }

        [Fact]
        public void DropValue_Works()
        {
            // Arrange
            var source = SignalFactory.Default.CreateSignal<int>(7);
            var dropped = source.DropValue();

            // Whenever `dropped` changes, `derived` is updated with the latest value in `source`.
            var derived = dropped.Select(_ => source.Value);

            // Act
            derived.Value.Should().Be(7);

            source.Value = 3;
            derived.Value.Should().Be(3);
        }

        [Fact]
        public void AnyChange_Works()
        {
            // Arrange
            var a = SignalFactory.Default.CreateSignal<int>(1);
            var b = SignalFactory.Default.CreateSignal<int>(2);
            var c = SignalFactory.Default.CreateSignal<int>(3);

            var derived = new[] { a, b, c }.AnyChange();

            // Act
            derived.Value.Should().Be(1);

            a.Value = 2;
            derived.Value.Should().Be(2);

            b.Value = 5;
            derived.Value.Should().Be(5);

            c.Value = 4;
            derived.Value.Should().Be(4);

            a.Value = 9;
            derived.Value.Should().Be(9);
        }

        [Fact]
        public void AnyChange_NoSources_ReturnsNull()
        {
            // Act
            var signal = new IReadableSignal<Unit>[0].AnyChange();

            // Assert
            signal.Should().BeNull();
        }

        [Fact]
        public void AnyChange_MismatchedContext_Throws()
        {
            // Arrange
            var signal1 = new SignalFactory().CreateSignal<int>();
            var signal2 = new SignalFactory().CreateSignal<int>();

            // Act
            Action action = () => new[] { signal1, signal2 }.AnyChange();

            // Assert
            action.Should().ThrowExactly<InvalidOperationException>()
                .WithMessage("*Cannot mix signals*");
        }

        [Fact]
        public void AnyChange_ThreadSafety_Works()
        {
            // Arrange
            const int Iterations = 100_000;

            var a = SignalFactory.Default.CreateSignal<int>(1);
            var b = SignalFactory.Default.CreateSignal<int>(2);
            var c = SignalFactory.Default.CreateSignal<int>(3);

            // Act
            var count = 0;
            var derived = new[] { a, b, c }.AnyChange()
                .Select(i => count++);

            Parallel.For(0, Iterations, i =>
            {
                switch (i % 3)
                {
                    case 0:
                        a.Value = i;
                        break;
                    case 1:
                        b.Value = i;
                        break;
                    case 2:
                        c.Value = i;
                        break;
                }
            });

            count.Should().Be(Iterations + 1);
        }

        private class Item
        {
            public Item(int id)
            {
                Id = id;
            }

            public int Id { get; }
        }
    }
}
