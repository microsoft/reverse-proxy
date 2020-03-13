// <copyright file="ThreadStaticRandom.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;

namespace IslandGateway.Utilities
{
    /// <summary>
    /// Provides a thread static implementation of random numbers that optimizes not to lock on every invocation of random number generation.
    /// </summary>
    public class ThreadStaticRandom
    {
        /// <summary>
        /// This is the shared instance of <see cref="RandomWrapper"/> that would be used to generate a seed value for the Thread static instance.
        /// </summary>
        private static readonly Lazy<RandomWrapper> _globalRandom = new Lazy<RandomWrapper>(() => new RandomWrapper(new Random()));

        /// <summary>
        /// This instance of <see cref="RandomWrapper"/> is unique to each thread.
        /// </summary>
        [ThreadStatic]
        private static RandomWrapper _threadLocalRandom = null;

        /// <summary>
        /// Gets the a thread safe instance of <see cref="RandomWrapper"/>.
        /// </summary>
        public static IRandom Instance
        {
            get
            {
                RandomWrapper currentInstance = _threadLocalRandom;

                // Check if for the current thread the seed has already been established. If not then lock on the global random instance to generate a seed value
                if (currentInstance == null)
                {
                    int seedForThreadLocalInstance;

                    lock (_globalRandom.Value)
                    {
                        seedForThreadLocalInstance = _globalRandom.Value.Next();
                    }

                    // Initialize the current instance with the seed
                    var random = new Random(seedForThreadLocalInstance);
                    currentInstance = new RandomWrapper(random);
                    _threadLocalRandom = currentInstance;
                }

                return currentInstance;
            }
        }
    }
}
