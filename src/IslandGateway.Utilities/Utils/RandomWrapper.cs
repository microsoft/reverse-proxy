// <copyright file="RandomWrapper.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

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
            this._random = random;
        }

        /// <inheritdoc/>
        public int Next()
        {
            return this._random.Next();
        }

        /// <inheritdoc/>
        public int Next(int maxValue)
        {
            return this._random.Next(maxValue);
        }

        /// <inheritdoc/>
        public int Next(int minValue, int maxValue)
        {
            return this._random.Next(minValue, maxValue);
        }
    }
}
