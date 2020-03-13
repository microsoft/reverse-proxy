// <copyright file="Signal.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Threading;

namespace IslandGateway.Signals
{
    /// <summary>
    /// Represents a signal that holds a value of type <typeparamref name="T"/>
    /// and serves as a notification mechanism when the value changes.
    /// This is especially useful with immutable type <typeparamref name="T"/>
    /// so that once a reference is obtained from this class using <see cref="Value"/>
    /// or <see cref="GetSnapshot"/>, the value can be used without worries of concurrent changes.
    /// </summary>
    /// <typeparam name="T">Type of the stored value.</typeparam>
    public class Signal<T> : IReadableSignal<T>, IWritableSignal<T>
    {
        private Snapshot _snapshot;

        /// <summary>
        /// Initializes a new instance of the <see cref="Signal{T}"/> class.
        /// </summary>
        internal Signal(SignalContext context)
        {
            this.Context = context;
            this._snapshot = new Snapshot(default);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Signal{T}"/> class.
        /// </summary>
        internal Signal(SignalContext context, T value)
        {
            this.Context = context;
            this._snapshot = new Snapshot(value);
        }

        /// <inheritdoc/>
        public SignalContext Context { get; }

        /// <summary>
        /// Gets or sets the value held by this signal.
        /// </summary>
        public T Value
        {
            get => Volatile.Read(ref this._snapshot).Value;
            set
            {
                this.Context.QueueAction(() =>
                {
                    var currentSnapshot = this._snapshot;
                    Volatile.Write(ref this._snapshot, new Snapshot(value));
                    currentSnapshot.Notify();
                });
            }
        }

        /// <inheritdoc/>
        public ISignalSnapshot<T> GetSnapshot() => Volatile.Read(ref this._snapshot);

        private class Snapshot : ISignalSnapshot<T>
        {
            private bool _changed;

            public Snapshot(T value)
            {
                this.Value = value;
            }

            private event Action ChangedEvent;

            /// <inheritdoc/>
            public T Value { get; }

            /// <inheritdoc/>
            public IDisposable OnChange(Action action)
            {
                this.ChangedEvent += action;
                if (Volatile.Read(ref this._changed))
                {
                    action();
                }

                return new UnsubscribeDisposable(() => this.ChangedEvent -= action);
            }

            internal void Notify()
            {
                Volatile.Write(ref this._changed, true);
                this.ChangedEvent?.Invoke();
            }

            private class UnsubscribeDisposable : IDisposable
            {
                private Action _disposeAction;

                public UnsubscribeDisposable(Action disposeAction)
                {
                    this._disposeAction = disposeAction;
                }

                public void Dispose()
                {
                    this._disposeAction?.Invoke();
                    this._disposeAction = null;
                }
            }
        }
    }
}
