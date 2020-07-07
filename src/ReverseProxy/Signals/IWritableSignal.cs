// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Signals
{
    /// <summary>
    /// Represents a signal whose value can be set at any time, and
    /// which can interoperate with any other signals belonging to the same
    /// <see cref="SignalContext"/>.
    /// </summary>
    /// <typeparam name="T">Type of the stored value.</typeparam>
    internal interface IWritableSignal<in T> : ISignal
    {
        /// <summary>
        /// Sets the current value and broadcasts to consumers that the signal has changed.
        /// </summary>
        T Value { set; }
    }
}
