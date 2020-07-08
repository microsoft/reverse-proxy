// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Signals
{
    /// <summary>
    /// Snapshot of a <see cref="Signal{T}"/> allowing to subscribe to state changes.
    /// </summary>
    /// <typeparam name="T">Type of the stored value.</typeparam>
    internal interface ISignalSnapshot<out T>
    {
        /// <summary>
        /// Gets the value when the snapshot was taken.
        /// </summary>
        T Value { get; }

        /// <summary>
        /// Registers an action to be executed when the current snapshot
        /// ceases to be the latest snapshot.
        /// If the snapshot already isn't the latest, the callback <paramref name="action"/>
        /// is called immediately.
        /// This has the property that it is called **at least once**, and in rare cases
        /// it could be called twice for the same update.
        /// </summary>
        IDisposable OnChange(Action action);
    }
}
