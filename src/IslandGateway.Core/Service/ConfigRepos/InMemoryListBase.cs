// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace IslandGateway.Core.Service
{
    internal abstract class InMemoryListBase<T>
        where T : IDeepCloneable<T>
    {
        private readonly object _syncRoot = new object();
        private IList<T> _items;

        protected IList<T> Get()
        {
            lock (_syncRoot)
            {
                return _items?.DeepClone();
            }
        }

        protected void Set(IList<T> items)
        {
            lock (_syncRoot)
            {
                _items = items?.DeepClone();
            }
        }
    }
}
