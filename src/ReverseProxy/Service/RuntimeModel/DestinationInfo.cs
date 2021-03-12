// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Representation of a cluster's destination for use at runtime.
    /// </summary>
    /// <remarks>
    /// Note that while this class is immutable, specific members such as
    /// <see cref="Config"/> and <see cref="Health"/> hold mutable references
    /// that can be updated and which will always have latest information
    /// relevant to this endpoint.
    /// All members are thread safe.
    /// </remarks>
    public sealed class DestinationInfo : IReadOnlyList<DestinationInfo>
    {
        private volatile DestinationConfig _config;

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
        /// A snapshot of the current configuration
        /// </summary>
        public DestinationConfig Config
        {
            get => _config;
            internal set => _config = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Mutable health state for this destination.
        /// </summary>
        public DestinationHealthState Health { get; } = new DestinationHealthState();

        /// <summary>
        /// Keeps track of the total number of concurrent requests on this endpoint.
        /// </summary>
        public int ConcurrentRequestCount => ConcurrencyCounter.Value;

        internal AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();

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
