// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Management
{
    /// <summary>
    /// Default implementation of <see cref="IDestinationManagerFactory"/>
    /// which creates instances of <see cref="DestinationManager"/>
    /// to manage destinations of a cluster at runtime.
    /// </summary>
    internal class DestinationManagerFactory : IDestinationManagerFactory
    {
        private readonly IReadOnlyList<IDestinationChangeListener> _changeListeners;

        public DestinationManagerFactory(IEnumerable<IDestinationChangeListener> changeListeners)
        {
            _changeListeners = changeListeners?.ToArray() ?? Array.Empty<IDestinationChangeListener>();
        }

        /// <inheritdoc/>
        public IDestinationManager CreateDestinationManager()
        {
            return new DestinationManager(_changeListeners);
        }
    }
}
