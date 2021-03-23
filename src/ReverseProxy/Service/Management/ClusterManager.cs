// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.Management
{
    internal sealed class ClusterManager : ItemManagerBase<ClusterInfo>, IClusterManager
    {
        private readonly IReadOnlyList<IClusterChangeListener> _changeListeners;

        public ClusterManager(IEnumerable<IClusterChangeListener> changeListeners)
        {
            _changeListeners = changeListeners?.ToArray() ?? Array.Empty<IClusterChangeListener>();
        }

        protected override void OnItemRemoved(ClusterInfo item)
        {
            foreach (var changeListener in _changeListeners)
            {
                changeListener.OnClusterRemoved(item);
            }
        }

        protected override void OnItemChanged(ClusterInfo item, bool added)
        {
            foreach (var changeListener in _changeListeners)
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
            return new ClusterInfo(itemId);
        }
    }
}
