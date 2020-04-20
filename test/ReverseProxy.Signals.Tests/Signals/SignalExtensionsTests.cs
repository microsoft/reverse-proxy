// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
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
            Assert.Equal(-1, derived.Value);

            signal.Value = new Item(1);
            Assert.Equal(1, derived.Value);

            signal.Value = new Item(7);
            Assert.Equal(7, derived.Value);

            signal.Value = null;
            Assert.Equal(-1, derived.Value);
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

            Assert.Equal(0, x.Value);

            a.Value = 1;
            Assert.Equal(1, x.Value);

            selector.Value = b;
            Assert.Equal(2, x.Value);
            var notified = false;
            x.GetSnapshot().OnChange(() => notified = true);

            a.Value = 3;
            Assert.Equal(2, x.Value);
            Assert.False(notified);

            selector.Value = null;
            Assert.Equal(0, x.Value);
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
            Assert.Throws<InvalidOperationException>(action);
        }

        [Fact]
        public void SelectMany_Works()
        {
            // Arrange
            var a = SignalFactory.Default.CreateSignal<int>(1);
            var b = SignalFactory.Default.CreateSignal<int>(2);
            var x = a.SelectMany(a_ => b.Select(b_ => (aVal: a_, bVal: b_)));

            // Act & Assert
            Assert.Equal(1, x.Value.aVal);
            Assert.Equal(2, x.Value.bVal);

            a.Value = 3;
            Assert.Equal(3, x.Value.aVal);
            Assert.Equal(2, x.Value.bVal);

            b.Value = 4;
            Assert.Equal(3, x.Value.aVal);
            Assert.Equal(4, x.Value.bVal);
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
            Assert.Equal(7, derived.Value);

            source.Value = 3;
            Assert.Equal(3, derived.Value);
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
            Assert.Equal(1, derived.Value);

            a.Value = 2;
            Assert.Equal(2, derived.Value);

            b.Value = 5;
            Assert.Equal(5, derived.Value);

            c.Value = 4;
            Assert.Equal(4, derived.Value);

            a.Value = 9;
            Assert.Equal(9, derived.Value);
        }

        [Fact]
        public void AnyChange_NoSources_ReturnsNull()
        {
            // Act
            var signal = new IReadableSignal<Unit>[0].AnyChange();

            // Assert
            Assert.Null(signal);
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
            Assert.Throws<InvalidOperationException>(action);
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

            Assert.Equal(Iterations + 1, count);
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
