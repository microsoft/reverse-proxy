// <copyright file="SignalFactory.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Signals
{
    /// <summary>
    /// Class used to create instances of <see cref="Signal{T}"/>
    /// within the same <see cref="SignalContext"/>.
    /// </summary>
    public sealed class SignalFactory
    {
        private readonly SignalContext context;

        /// <summary>
        /// Initializes a new instance of the <see cref="SignalFactory"/> class.
        /// </summary>
        public SignalFactory()
        {
            this.context = new SignalContext();
        }

        /// <summary>
        /// Gets the default instance of <see cref="SignalFactory"/>.
        /// This can be used in simple scenarios where all signals
        /// should share the same <see cref="SignalContext"/>.
        /// </summary>
        public static SignalFactory Default { get; } = new SignalFactory();

        /// <summary>
        /// Creates a new <see cref="Signal{T}"/>.
        /// </summary>
        public Signal<T> CreateSignal<T>()
        {
            return new Signal<T>(this.context);
        }

        /// <summary>
        /// Creates a new <see cref="Signal{T}"/> with the provided initial value.
        /// </summary>
        public Signal<T> CreateSignal<T>(T value)
        {
            return new Signal<T>(this.context, value);
        }

        /// <summary>
        /// Creates a new signal to be used as a notifications device,
        /// carrying the default <see cref="Unit"/> value.
        /// </summary>
        /// <remarks>
        /// This is the closest we have to a typeless `Signal` instead o `Signal{T}`.
        /// </remarks>
        public Signal<Unit> CreateUnitSignal()
        {
            return new Signal<Unit>(this.context, Unit.Instance);
        }
    }
}
