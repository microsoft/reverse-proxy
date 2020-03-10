// <copyright file="AtomicCounter.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Threading;

namespace IslandGateway.Core.Util
{
    internal class AtomicCounter
    {
        private int value;

        /// <summary>
        /// Gets the current value of the counter.
        /// </summary>
        public int Value => Volatile.Read(ref this.value);

        /// <summary>
        /// Atomically increments the counter value by 1.
        /// </summary>
        public void Increment()
        {
            Interlocked.Increment(ref this.value);
        }

        /// <summary>
        /// Atomically decrements the counter value by 1.
        /// </summary>
        public void Decrement()
        {
            Interlocked.Decrement(ref this.value);
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
                int val = Volatile.Read(ref this.value);
                if (val >= max)
                {
                    return false;
                }
                if (Interlocked.CompareExchange(ref this.value, val + 1, val) == val)
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
                int val = Volatile.Read(ref this.value);
                if (val <= min)
                {
                    return false;
                }
                if (Interlocked.CompareExchange(ref this.value, val - 1, val) == val)
                {
                    return true;
                }
            }
        }
    }
}
