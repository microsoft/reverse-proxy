// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Signals
{
    /// <summary>
    /// Dummy value which can be used as a type parameter with <see cref="Signal{T}"/>
    /// to create a value-less signal, useful to propagate notification events.
    /// </summary>
    public sealed class Unit
    {
        /// <summary>
        /// Gets the singleton instance of <see cref="Unit"/>.
        /// </summary>
        public static readonly Unit Instance = new Unit();

        private Unit()
        {
        }
    }
}
