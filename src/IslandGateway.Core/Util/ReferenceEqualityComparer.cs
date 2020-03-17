// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace IslandGateway.Core.Util
{
    /// <summary>
    /// A comparer based on the object reference equality only.
    /// </summary>
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    {
        private ReferenceEqualityComparer()
        {
        }

        public static ReferenceEqualityComparer<T> Default { get; } = new ReferenceEqualityComparer<T>();

        /// <inheritdoc/>
        public bool Equals(T x, T y) => ReferenceEquals(x, y);

        /// <inheritdoc/>
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
