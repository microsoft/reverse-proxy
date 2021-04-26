// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

namespace Yarp.ReverseProxy.Utilities
{
    internal sealed class AtomicCounter
    {
        private ThreadLocal<long> _values = new(trackAllValues: true);

        /// <summary>
        /// Gets the current value of the counter.
        /// </summary>
        /// <remarks>
        /// Note: getting the value is allocating.
        /// </remarks>
        public long Value
        {
            get
            {
                var sum = 0L;
                foreach (var value in _values.Values)
                {
                    sum += value;
                }

                return sum;
            }
            set
            {
                var values = new ThreadLocal<long>(trackAllValues: true)
                {
                    Value = value
                };

                Volatile.Write(ref _values, values);
            }
        }

        /// <summary>
        /// Atomically increments the counter value by 1.
        /// </summary>
        public void Increment()
        {
            _values.Value++;
        }

        /// <summary>
        /// Atomically increments the counter value by 1.
        /// </summary>
        /// <remarks>
        /// Note: getting the value is allocating.
        /// </remarks>
        public long IncrementAndGetValue()
        {
            _values.Value++;
            return Value;
        }

        /// <summary>
        /// Atomically decrements the counter value by 1.
        /// </summary>
        public void Decrement()
        {
            _values.Value--;
        }

        /// <summary>
        /// Atomically resets the counter value to 0.
        /// </summary>
        public void Reset()
        {
            var values = new ThreadLocal<long>(trackAllValues: true);
            Volatile.Write(ref _values, values);
        }
    }
}
