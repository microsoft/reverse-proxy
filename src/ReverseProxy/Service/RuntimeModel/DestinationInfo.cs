// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Signals;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Representation of a cluster's destination for use at runtime.
    /// </summary>
    /// <remarks>
    /// Note that while this class is immutable, specific members such as
    /// <see cref="Config"/> and <see cref="DynamicState"/> hold mutable references
    /// that can be updated atomically and which will always have latest information
    /// relevant to this endpoint.
    /// All members are thread safe.
    /// </remarks>
    public sealed class DestinationInfo : IReadOnlyList<DestinationInfo>
    {
        private readonly Dictionary<object, object> _dynamicProperties = new Dictionary<object, object>();

        public DestinationInfo(string destinationId)
        {
            if (string.IsNullOrEmpty(destinationId))
            {
                throw new ArgumentNullException(nameof(destinationId));
            }
            DestinationId = destinationId;
        }

        public string DestinationId { get; }

        /// <summary>
        /// Encapsulates parts of an endpoint that can change atomically
        /// in reaction to config changes.
        /// </summary>
        internal Signal<DestinationConfig> ConfigSignal { get; } = SignalFactory.Default.CreateSignal<DestinationConfig>();

        /// <summary>
        /// A snapshot of the current configuration
        /// </summary>
        public DestinationConfig Config => ConfigSignal.Value;

        /// <summary>
        /// Encapsulates parts of an destination that can change atomically
        /// in reaction to runtime state changes (e.g. endpoint health states).
        /// </summary>
        internal Signal<DestinationDynamicState> DynamicStateSignal { get; } = SignalFactory.Default.CreateSignal<DestinationDynamicState>(new DestinationDynamicState(default));

        /// <summary>
        /// A snapshot of the current dynamic state.
        /// </summary>
        public DestinationDynamicState DynamicState
        {
            get => DynamicStateSignal.Value;
            set => DynamicStateSignal.Value = value;
        }

        /// <summary>
        /// Keeps track of the total number of concurrent requests on this endpoint.
        /// </summary>
        public int ConcurrentRequestCount => ConcurrencyCounter.Value;

        internal AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();

        // Temporary implementation of a simple thread-safe dynamic property bag.
        // It will be replaced later with something more advanced.
        internal TValue GetOrAddProperty<TKey, TValue>(TKey key, Func<TKey, TValue> valueFactory)
        {
            lock (_dynamicProperties)
            {
                if (!_dynamicProperties.TryGetValue(key, out var value))
                {
                    value = valueFactory(key);
                    _dynamicProperties[key] = value;
                }

                return (TValue) value;
            }
        }

        internal bool TryRemoveProperty<TKey>(TKey key)
        {
            lock (_dynamicProperties)
            {
                return _dynamicProperties.Remove(key);
            }
        }

        DestinationInfo IReadOnlyList<DestinationInfo>.this[int index]
            => index == 0 ? this : throw new IndexOutOfRangeException();

        int IReadOnlyCollection<DestinationInfo>.Count => 1;

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<DestinationInfo> IEnumerable<DestinationInfo>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct Enumerator : IEnumerator<DestinationInfo>
        {
            private bool _read;

            internal Enumerator(DestinationInfo destinationInfo)
            {
                Current = destinationInfo;
                _read = false;
            }

            public DestinationInfo Current { get; }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (!_read)
                {
                    _read = true;
                    return true;
                }
                return false;
            }

            public void Dispose()
            {

            }

            void IEnumerator.Reset()
            {
                throw new NotSupportedException();
            }
        }
    }
}
