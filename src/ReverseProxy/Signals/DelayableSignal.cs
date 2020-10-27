// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Signals
{
    ///<summary>
    /// It's a wrapper signal that always read the current value of a nested singal, but can delay writes to it.
    /// By default, all writes are sent directly to the nested signal, 
    /// so there is no difference externally observable behavior from the regular <see cref="Signal<T>"/>.
    /// However, once it gets switched to the delayed mode, writes don't change the nested signal's value, 
    /// but instead get postponed until some time later. It doesn't store the whole write history, but only the latest written value. 
    /// On switching back to the default pass-through mode, thst latest delayed value gets applied to the nested signal.
    ///</summary>
    internal class DelayableSignal<T> : IReadableSignal<T>, IWritableSignal<T>
    {
        private readonly Signal<T> _realSignal;
        private volatile bool _delayingWrites;
        private volatile bool _updateWasDelayed;
        private T _latestDelayedValue;
        private readonly object _syncRoot = new object();

        public DelayableSignal(SignalContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _realSignal = new Signal<T>(context);
        }

        public SignalContext Context => _realSignal.Context;

        public T Value
        {
            get => _realSignal.Value;
            set {
                if (_delayingWrites)
                {
                    lock (_syncRoot)
                    {
                        if (_delayingWrites)
                        {
                            _latestDelayedValue = value;
                            _updateWasDelayed = true;
                            return;
                        }
                    }
                }

                _realSignal.Value = value;
            }
        }

        public ISignalSnapshot<T> GetSnapshot() => _realSignal.GetSnapshot();

        public void Pause()
        {
            lock (_syncRoot)
            {
                _delayingWrites = true;
            }
        }

        public void Resume()
        {
            if (!_delayingWrites)
            {
                return;
            }

            T lastKnownValue;
            var applyLastKnownValue = false;
            lock (_syncRoot)
            {
                lastKnownValue = _latestDelayedValue;
                applyLastKnownValue = _updateWasDelayed;
                _delayingWrites = false;
                _updateWasDelayed = false;
                _latestDelayedValue = default;
            }

            if (applyLastKnownValue)
            {
                Value = lastKnownValue;
            }
        }
    }
}
