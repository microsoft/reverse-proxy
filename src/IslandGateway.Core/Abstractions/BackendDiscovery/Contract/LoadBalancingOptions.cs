// <copyright file="LoadBalancingOptions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// Load balancing options.
    /// </summary>
    public sealed class LoadBalancingOptions
    {
        internal LoadBalancingOptions DeepClone()
        {
            return new LoadBalancingOptions
            {
            };
        }
    }
}
