// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.ReverseProxy.Service.Management
{
    public abstract class ItemManagerBase<T> : IItemManager<T>
        where T : class
    {
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, T> _items = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        private volatile IReadOnlyList<T> _snapshot = Array.Empty<T>();

        /// <inheritdoc/>
        public IReadOnlyList<T> Items => _snapshot;

        /// <inheritdoc/>
        public T TryGetItem(string itemId)
        {
            _ = itemId ?? throw new ArgumentNullException(nameof(itemId));

            lock (_lockObject)
            {
                _items.TryGetValue(itemId, out var item);
                return item;
            }
        }

        /// <inheritdoc/>
        public T GetOrCreateItem(string itemId, Action<T> setupAction)
        {
            _ = itemId ?? throw new ArgumentNullException(nameof(itemId));
            _ = setupAction ?? throw new ArgumentNullException(nameof(setupAction));

            lock (_lockObject)
            {
                var existed = _items.TryGetValue(itemId, out var item);
                if (!existed)
                {
                    item = InstantiateItem(itemId);
                }

                setupAction(item);

                if (!existed)
                {
                    _items.Add(itemId, item);
                    UpdateSnapshot();
                }

                OnItemChanged(item, !existed);

                return item;
            }
        }

        /// <inheritdoc/>
        public IList<T> GetItems()
        {
            lock (_lockObject)
            {
                return _items.Values.ToList();
            }
        }

        /// <inheritdoc/>
        public bool TryRemoveItem(string itemId)
        {
            _ = itemId ?? throw new ArgumentNullException(nameof(itemId));

            lock (_lockObject)
            {
                var removed = _items.Remove(itemId, out var removedItem);

                if (removed)
                {
                    UpdateSnapshot();
                    OnItemRemoved(removedItem);
                }

                return removed;
            }
        }

        /// <summary>
        /// Creates a new item with the given <paramref name="itemId"/>.
        /// </summary>
        protected abstract T InstantiateItem(string itemId);

        protected virtual void OnItemChanged(T item, bool added)
        {}

        protected virtual void OnItemRemoved(T item)
        {}

        private void UpdateSnapshot()
        {
            _snapshot = _items.Select(kvp => kvp.Value).ToList().AsReadOnly();
        }
    }
}
