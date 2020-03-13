// <copyright file="TestAutoMockBase.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using Autofac.Core;
using Autofac.Extras.Moq;
using IslandGateway.Utilities;
using Moq;

namespace Tests.Common
{
    /// <summary>
    /// Automatically generates mocks for interfaces on the Class under test.
    /// </summary>
    public class TestAutoMockBase : IDisposable
    {
        private bool _isDisposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestAutoMockBase" /> class.
        /// </summary>
        public TestAutoMockBase()
        {
            this.AutoMock = AutoMock.GetLoose();
        }

        /// <summary>
        /// Gets the mocks.
        /// </summary>
        protected AutoMock AutoMock { get; private set; }

        /// <summary>
        /// Resets the mocks.
        /// </summary>
        public void ResetMocks()
        {
            this.AutoMock.Dispose();
            this.AutoMock = AutoMock.GetLoose();
        }

        /// <summary>
        /// Creates an object of <typeparamref name="TService"/> using the dependency container.
        /// </summary>
        /// <typeparam name="TService">The type of the object to create.</typeparam>
        /// <param name="parameters">The parameters.</param>
        /// <returns>
        /// Instance of <typeparamref name="TService"/>.
        /// </returns>
        public virtual TService Create<TService>(params Parameter[] parameters)
            where TService : class
        {
            return this.AutoMock.Create<TService>(parameters);
        }

        /// <summary>
        /// Creates a mock of the specified abstraction.
        /// </summary>
        /// <typeparam name="TDependencyToMock">The type of the dependency to mock.</typeparam>
        /// <returns>A mock of the type.</returns>
        public Mock<TDependencyToMock> Mock<TDependencyToMock>()
            where TDependencyToMock : class
        {
            return this.AutoMock.Mock<TDependencyToMock>();
        }

        /// <summary>
        /// Provide the specified abstraction to the dependency injection container.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <param name="instance">The instance.</param>
        public void Provide<TService>(TService instance)
            where TService : class
        {
            Contracts.CheckValue(instance, nameof(instance));
            this.AutoMock.Provide(instance);
        }

        /// <summary>
        /// Provide the specified concrete type to the dependency injection container.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <typeparam name="TImplementation">The type that implements the service.</typeparam>
        /// <returns>An instance of <typeparamref name="TImplementation"/> that implements <typeparamref name="TService"/>.</returns>
        public TImplementation Provide<TService, TImplementation>()
            where TService : class
            where TImplementation : TService
        {
            return (TImplementation)this.AutoMock.Provide<TService, TImplementation>();
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            if (!this._isDisposed)
            {
                this.AutoMock.Dispose();
                this._isDisposed = true;
            }
        }
    }
}
