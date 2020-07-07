// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Microsoft.ReverseProxy.Signals
{
    /// <summary>
    /// Represents a signal that holds a value of type <typeparamref name="T"/>
    /// and serves as a notification mechanism when the value changes.
    /// This is especially useful with immutable type <typeparamref name="T"/>
    /// so that once a reference is obtained from this class using <see cref="Value"/>
    /// or <see cref="GetSnapshot"/>, the value can be used without worries of concurrent changes.
    /// </summary>
    /// <typeparam name="T">Type of the stored value.</typeparam>
    internal class Signal<T> : IReadableSignal<T>, IWritableSignal<T>
    {
        private Snapshot _snapshot;

        /// <summary>
        /// Initializes a new instance of the <see cref="Signal{T}"/> class.
        /// </summary>
        internal Signal(SignalContext context)
        {
            Context = context;
            _snapshot = new Snapshot(default);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Signal{T}"/> class.
        /// </summary>
        internal Signal(SignalContext context, T value)
        {
            Context = context;
            _snapshot = new Snapshot(value);
        }

        /// <inheritdoc/>
        public SignalContext Context { get; }

        /// <summary>
        /// Gets or sets the value held by this signal.
        /// </summary>
        public T Value
        {
            get => Volatile.Read(ref _snapshot).Value;
            set
            {
                Context.QueueAction(() =>
                {
                    var currentSnapshot = _snapshot;
                    Volatile.Write(ref _snapshot, new Snapshot(value));
                    currentSnapshot.Notify();
                });
            }
        }

        /// <inheritdoc/>
        public ISignalSnapshot<T> GetSnapshot() => Volatile.Read(ref _snapshot);

        private class Snapshot : ISignalSnapshot<T>
        {
            private bool _changed;

            public Snapshot(T value)
            {
                Value = value;
            }

            private event Action ChangedEvent;

            /// <inheritdoc/>
            public T Value { get; }

            /// <inheritdoc/>
            public IDisposable OnChange(Action action)
            {
                ChangedEvent += action;
                if (Volatile.Read(ref _changed))
                {
                    action();
                }

                return new UnsubscribeDisposable(() => ChangedEvent -= action);
            }

            internal void Notify()
            {
                Volatile.Write(ref _changed, true);
                ChangedEvent?.Invoke();
            }

            private class UnsubscribeDisposable : IDisposable
            {
                private Action _disposeAction;

                public UnsubscribeDisposable(Action disposeAction)
                {
                    _disposeAction = disposeAction;
                }

                public void Dispose()
                {
                    _disposeAction?.Invoke();
                    _disposeAction = null;
                }
            }
        }
    }
}
