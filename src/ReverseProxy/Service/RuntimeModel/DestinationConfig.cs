// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a destination
    /// that only change in reaction to configuration changes
    /// (e.g. address).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="DestinationConfig"/> are replaced
    /// in ther entirety when values need to change.
    /// </remarks>
    public sealed class DestinationConfig
    {
        public DestinationConfig(Destination destination)
        {
            Options = destination ?? throw new ArgumentNullException(nameof(destination));
        }

        public Destination Options { get; }

        internal bool HasChanged(Destination destination)
        {
            return Options != destination;
        }
    }
}
