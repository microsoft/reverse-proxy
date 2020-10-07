// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Management
{
    internal sealed class ClusterManager : ItemManagerBase<ClusterInfo>, IClusterManager
    {
        private readonly IDestinationManagerFactory _destinationManagerFactory;
        private readonly IReadOnlyList<IClusterChangeListener> _changeListeners;

        public ClusterManager(IDestinationManagerFactory destinationManagerFactory, IEnumerable<IClusterChangeListener> changeListeners)
        {
            _destinationManagerFactory = destinationManagerFactory ?? throw new ArgumentNullException(nameof(destinationManagerFactory));
            _changeListeners = changeListeners?.ToArray() ?? new IClusterChangeListener[0];
        }

        protected override void OnItemRemoved(ClusterInfo item)
        {
            foreach(var changeListener in _changeListeners)
            {
                changeListener.OnClusterRemoved(item);
            }
        }

        protected override void OnItemChanged(ClusterInfo item, bool added)
        {
            foreach(var changeListener in _changeListeners)
            {
                if (added)
                {
                    changeListener.OnClusterAdded(item);
                }
                else
                {
                    changeListener.OnClusterChanged(item);
                }
            }
        }

        /// <inheritdoc/>
        protected override ClusterInfo InstantiateItem(string itemId)
        {
            var destinationManager = _destinationManagerFactory.CreateDestinationManager();
            return new ClusterInfo(itemId, destinationManager);
        }
    }
}
