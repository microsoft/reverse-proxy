// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace IslandGateway.Utilities
{
    /// <summary>
    /// Wrapper around <see cref="Random"/> that facilitates deterministic unit testing.
    /// </summary>
    public class RandomWrapper : IRandom
    {
        private readonly Random _random;

        /// <summary>
        /// Initializes a new instance of the <see cref="RandomWrapper"/> class.
        /// </summary>
        public RandomWrapper(Random random)
        {
            Contracts.CheckValue(random, nameof(random));
            _random = random;
        }

        /// <inheritdoc/>
        public int Next()
        {
            return _random.Next();
        }

        /// <inheritdoc/>
        public int Next(int maxValue)
        {
            return _random.Next(maxValue);
        }

        /// <inheritdoc/>
        public int Next(int minValue, int maxValue)
        {
            return _random.Next(minValue, maxValue);
        }
    }
}
