// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

namespace Microsoft.ReverseProxy.Utilities
{
    public class AtomicCounter
    {
        private int _value;

        /// <summary>
        /// Gets the current value of the counter.
        /// </summary>
        public int Value {
            get => Volatile.Read(ref _value);
            set => Volatile.Write(ref _value, value);
        }

        /// <summary>
        /// Atomically increments the counter value by 1.
        /// </summary>
        public int Increment()
        {
            return Interlocked.Increment(ref _value);
        }

        /// <summary>
        /// Atomically decrements the counter value by 1.
        /// </summary>
        public int Decrement()
        {
            return Interlocked.Decrement(ref _value);
        }

        /// <summary>
        /// Atomically increments the counter value by 1, but only up to the
        /// specified <paramref name="max"/>.
        /// </summary>
        /// <param name="max">Maximum value to increment to.</param>
        /// <returns>True if the value was incremented; false otherwise.</returns>
        public bool IncrementCapped(int max)
        {
            while (true)
            {
                var val = Volatile.Read(ref _value);
                if (val >= max)
                {
                    return false;
                }
                if (Interlocked.CompareExchange(ref _value, val + 1, val) == val)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Atomically decrements the counter value by 1, but only down to the
        /// specified <paramref name="min"/>.
        /// </summary>
        /// <param name="min">Minimum value to decrement to.</param>
        /// <returns>True if the value was decremented; false otherwise.</returns>
        public bool DecrementCapped(int min)
        {
            while (true)
            {
                var val = Volatile.Read(ref _value);
                if (val <= min)
                {
                    return false;
                }
                if (Interlocked.CompareExchange(ref _value, val - 1, val) == val)
                {
                    return true;
                }
            }
        }
    }
}
