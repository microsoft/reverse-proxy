// <copyright file="Contracts.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;

namespace IslandGateway.CoreServicesBorrowed
{
    /// <summary>
    /// Simple argument validation helpers.
    /// </summary>
    // TODO: 6106627: find suitable replacement for CoreServicesBorrowed.Contracts
    public static class Contracts
    {
        /// <summary>
        /// Verifies that <paramref name="value"/> is not null, and throws
        /// <see cref="ArgumentNullException"/> otherwise.
        /// </summary>
        public static void CheckValue<T>(T value, string paramName)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        /// <summary>
        /// Verifies that <paramref name="value"/> is not null nor empty, and throws
        /// <see cref="ArgumentNullException"/> otherwise.
        /// </summary>
        public static void CheckNonEmpty(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(paramName);
            }
        }

        /// <summary>
        /// Verifies that <paramref name="value"/> is true, and throws
        /// <see cref="ArgumentException"/> otherwise.
        /// </summary>
        public static void Check(bool value, string paramName)
        {
            if (!value)
            {
                throw new ArgumentException(paramName);
            }
        }
    }
}
