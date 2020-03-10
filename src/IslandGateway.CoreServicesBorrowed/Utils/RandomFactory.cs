// <copyright file="RandomFactory.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;

namespace IslandGateway.CoreServicesBorrowed
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
