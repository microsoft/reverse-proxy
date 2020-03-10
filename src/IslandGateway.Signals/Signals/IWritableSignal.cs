// <copyright file="IWritableSignal.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Signals
{
    /// <summary>
    /// Represents a signal whose value can be set at any time, and
    /// which can interoperate with any other signals belonging to the same
    /// <see cref="SignalContext"/>.
    /// </summary>
    /// <typeparam name="T">Type of the stored value.</typeparam>
    public interface IWritableSignal<in T> : ISignal
    {
        /// <summary>
        /// Sets the current value and broadcasts to consumers that the signal has changed.
        /// </summary>
        T Value { set; }
    }
}
