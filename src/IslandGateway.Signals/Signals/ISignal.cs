// <copyright file="ISignal.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Signals
{
    /// <summary>
    /// All signals implement this interface, which includes a property
    /// <see cref="Context"/> indicating to which context the signal belongs.
    /// Signals from different contexts cannot be mixed due to thread safety concerns.
    /// </summary>
    public interface ISignal
    {
        /// <summary>
        /// Context of the signal. Writes to signals within the same context
        /// are always sequentialized to ensure thread safety.
        /// </summary>
        SignalContext Context { get; }
    }
}
