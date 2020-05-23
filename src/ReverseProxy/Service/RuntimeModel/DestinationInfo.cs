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
    /// Representation of a backend's destination for use at runtime.
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
        public DestinationInfo(string destinationId)
        {
            Contracts.CheckNonEmpty(destinationId, nameof(destinationId));
            DestinationId = destinationId;
        }

        public string DestinationId { get; }

        /// <summary>
        /// Encapsulates parts of an endpoint that can change atomically
        /// in reaction to config changes.
        /// </summary>
        public Signal<DestinationConfig> Config { get; } = SignalFactory.Default.CreateSignal<DestinationConfig>();

        /// <summary>
        /// Encapsulates parts of an destination that can change atomically
        /// in reaction to runtime state changes (e.g. endpoint health states).
        /// </summary>
        public Signal<DestinationDynamicState> DynamicState { get; } = SignalFactory.Default.CreateSignal<DestinationDynamicState>();

        /// <summary>
        /// Keeps track of the total number of concurrent requests on this endpoint.
        /// </summary>
        public AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();

        DestinationInfo IReadOnlyList<DestinationInfo>.this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return this;
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }

        int IReadOnlyCollection<DestinationInfo>.Count => 1;

        IEnumerator<DestinationInfo> IEnumerable<DestinationInfo>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public struct Enumerator : IEnumerator<DestinationInfo>
        {
            private bool _read;

            public Enumerator(DestinationInfo destinationInfo)
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

            public void Reset()
            {

            }
        }
    }
}
