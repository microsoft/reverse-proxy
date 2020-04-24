// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Utilities
{
    /// <summary>
    /// Factory for creating random class. This factory let us able to inject random class into other class.
    /// So that we can mock the random class for unit test.
    /// </summary>
    public interface IRandomFactory
    {
        /// <summary>
        /// Create a instance of random class.
        /// </summary>
        Random CreateRandomInstance();
    }
}
