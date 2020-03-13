// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace IslandGateway.Utilities
{
    /// <inheritdoc/>
    public class RandomFactory : IRandomFactory
    {
        /// <inheritdoc/>
        public IRandom CreateRandomInstance()
        {
            return ThreadStaticRandom.Instance;
        }
    }
}
