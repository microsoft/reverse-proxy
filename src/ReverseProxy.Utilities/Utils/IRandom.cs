// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Utilities
{
    /// <summary>
    /// Abstraction for generating random numbers. This interface is also helping us to unit test random generation.
    /// </summary>
    public interface IRandom
    {
        /// <summary>
        /// Returns a non-negative random integer.
        /// </summary>
        int Next();

        /// <summary>
        /// Generates a random number between zero (inclusive)
        /// and <paramref name="maxValue"/> (exclusive).
        /// </summary>
        int Next(int maxValue);

        /// <summary>
        /// Generates a random number between <paramref name="minValue"/> (inclusive)
        /// and <paramref name="maxValue"/> (exclusive).
        /// </summary>
        int Next(int minValue, int maxValue);
    }
}
