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
    public sealed class DestinationState : IReadOnlyList<DestinationState>
    {
        private volatile DestinationModel _model;

        /// <summary>
        /// Creates a new instance. This constructor is for tests and infrastructure, this type is normally constructed by
        /// the configuration loading infrastructure.
        /// </summary>
        public DestinationState(string destinationId)
        {
            if (string.IsNullOrEmpty(destinationId))
            {
                throw new ArgumentNullException(nameof(destinationId));
            }
            DestinationId = destinationId;
        }

        /// <summary>
        /// The destination's unique id.
        /// </summary>
        public string DestinationId { get; }

        /// <summary>
        /// A snapshot of the current configuration
        /// </summary>
        public DestinationModel Model
        {
            get => _model;
            internal set => _model = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Mutable health state for this destination.
        /// </summary>
        public DestinationHealthState Health { get; } = new DestinationHealthState();

        /// <summary>
        /// Keeps track of the total number of concurrent requests on this endpoint.
        /// The setter should only be used for testing purposes.
        /// </summary>
        public int ConcurrentRequestCount
        {
            get => ConcurrencyCounter.Value;
            set => ConcurrencyCounter.Value = value;
        }

        internal AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();

        /// <inheritdoc/>
        DestinationState IReadOnlyList<DestinationState>.this[int index]
            => index == 0 ? this : throw new IndexOutOfRangeException();

        /// <inheritdoc/>
        int IReadOnlyCollection<DestinationState>.Count => 1;

        private Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <inheritdoc/>
        IEnumerator<DestinationState> IEnumerable<DestinationState>.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private struct Enumerator : IEnumerator<DestinationState>
        {
            private bool _read;

            internal Enumerator(DestinationState instance)
            {
                Current = instance;
                _read = false;
            }

            public DestinationState Current { get; }

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
