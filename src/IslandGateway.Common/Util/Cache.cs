// <copyright file="Cache.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

using IslandGateway.Common.Abstractions.Time;

namespace IslandGateway.Common.Util
{
    /// <summary>
    /// TODO.
    /// </summary>
    // TODO: do we want this to be thread safe?
    public class Cache<T>
    {
        private readonly TimeSpan expirationTimeOffset;
        private readonly IMonotonicTimer timer;
        private Dictionary<string, Expirable> cache = new Dictionary<string, Expirable>(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{T}"/> class.
        /// </summary>
        /// <param name="timer">A timer to use to track expirations.</param>
        /// <param name="expirationTimeOffset">The time it takes for cache values to expire.</param>
        public Cache(IMonotonicTimer timer, TimeSpan expirationTimeOffset)
        {
            this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
            this.expirationTimeOffset = expirationTimeOffset;
        }

        /// <summary>
        /// TODO.
        /// </summary>
        public T Get(string key)
        {
            bool present = this.TryGetValue(key, out T value);
            if (!present)
            {
                throw new KeyNotFoundException($"Key {key} is not present.");
            }
            return value;
        }

        /// <summary>
        /// TODO.
        /// </summary>
        public bool TryGetValue(string key, out T value)
        {
            bool present = this.cache.TryGetValue(key, out Expirable expirable);
            if (!present || expirable.Expired(this.timer))
            {
                value = default;
                if (present)
                {
                    // Take the oportunity to update internal state
                    this.cache.Remove(key);
                }
                return false;
            }
            value = expirable.Value;
            return true;
        }

        /// <summary>
        /// TODO.
        /// </summary>
        public void Set(string key, T value)
        {
            this.cache[key] = new Expirable(
                value: value,
                expirationTime: this.timer.CurrentTime.Add(this.expirationTimeOffset));
        }

        /// <summary>
        /// Looks for expired values in the cache and deletes them.
        /// </summary>
        public void Cleanup()
        {
            var toRemove = this.cache
                .Where(pair => pair.Value.Expired(this.timer))
                .Select(pair => pair.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                this.cache.Remove(key);
            }
        }

        private struct Expirable
        {
            internal Expirable(T value, TimeSpan expirationTime)
            {
                this.Value = value;
                this.ExpirationTime = expirationTime;
            }
            internal T Value { get; }
            internal TimeSpan ExpirationTime { get; }
            internal bool Expired(IMonotonicTimer timer) => this.ExpirationTime < timer.CurrentTime;
        }
    }
}