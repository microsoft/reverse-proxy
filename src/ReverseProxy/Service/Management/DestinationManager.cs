// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.Management
{
    internal sealed class DestinationManager : ItemManagerBase<DestinationInfo>, IDestinationManager
    {
        private readonly IReadOnlyList<IDestinationChangeListener> _changeListeners;

        public DestinationManager(IEnumerable<IDestinationChangeListener> changeListeners)
        {
            _changeListeners = changeListeners?.ToArray() ?? Array.Empty<IDestinationChangeListener>();
        }

        protected override void OnItemRemoved(DestinationInfo item)
        {
            foreach (var changeListener in _changeListeners)
            {
                changeListener.OnDestinationRemoved(item);
            }
        }

        protected override void OnItemChanged(DestinationInfo item, bool added)
        {
            foreach (var changeListener in _changeListeners)
            {
                if (added)
                {
                    changeListener.OnDestinationAdded(item);
                }
                else
                {
                    changeListener.OnDestinationChanged(item);
                }
            }
        }

        /// <inheritdoc/>
        protected override DestinationInfo InstantiateItem(string itemId)
        {
            return new DestinationInfo(itemId);
        }
    }
}
