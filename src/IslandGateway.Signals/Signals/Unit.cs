// <copyright file="Unit.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Signals
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
