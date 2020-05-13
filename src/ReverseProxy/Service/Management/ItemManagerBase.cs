// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Utilities;
using Microsoft.ReverseProxy.Signals;

namespace Microsoft.ReverseProxy.Service.Management
{
    internal abstract class ItemManagerBase<T> : IItemManager<T>
        where T : class
    {
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, T> _items = new Dictionary<string, T>(StringComparer.Ordinal);
        private readonly Signal<IReadOnlyList<T>> _signal = SignalFactory.Default.CreateSignal<IReadOnlyList<T>>(new List<T>().AsReadOnly());

        /// <inheritdoc/>
        public IReadableSignal<IReadOnlyList<T>> Items => _signal;

        /// <inheritdoc/>
        public T TryGetItem(string itemId)
        {
            Contracts.CheckNonEmpty(itemId, nameof(itemId));

            lock (_lockObject)
            {
                _items.TryGetValue(itemId, out var item);
                return item;
            }
        }

        /// <inheritdoc/>
        public T GetOrCreateItem(string itemId, Action<T> setupAction)
        {
            Contracts.CheckNonEmpty(itemId, nameof(itemId));
            Contracts.CheckValue(setupAction, nameof(setupAction));

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
                    UpdateSignal();
                }

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
            Contracts.CheckNonEmpty(itemId, nameof(itemId));

            lock (_lockObject)
            {
                var removed = _items.Remove(itemId);

                if (removed)
                {
                    UpdateSignal();
                }

                return removed;
            }
        }

        /// <summary>
        /// Creates a new item with the given <paramref name="itemId"/>.
        /// </summary>
        protected abstract T InstantiateItem(string itemId);

        private void UpdateSignal()
        {
            _signal.Value = _items.Select(kvp => kvp.Value).ToList().AsReadOnly();
        }
    }
}
