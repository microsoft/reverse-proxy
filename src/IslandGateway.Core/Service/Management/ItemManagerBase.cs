// <copyright file="ItemManagerBase.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using IslandGateway.Utilities;
using IslandGateway.Signals;

namespace IslandGateway.Core.Service.Management
{
    internal abstract class ItemManagerBase<T> : IItemManager<T>
        where T : class
    {
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, T> _items = new Dictionary<string, T>(StringComparer.Ordinal);
        private readonly Signal<IReadOnlyList<T>> _signal = SignalFactory.Default.CreateSignal<IReadOnlyList<T>>(new List<T>().AsReadOnly());

        /// <inheritdoc/>
        public IReadableSignal<IReadOnlyList<T>> Items => this._signal;

        /// <inheritdoc/>
        public T TryGetItem(string itemId)
        {
            Contracts.CheckNonEmpty(itemId, nameof(itemId));

            lock (this._lockObject)
            {
                this._items.TryGetValue(itemId, out var item);
                return item;
            }
        }

        /// <inheritdoc/>
        public T GetOrCreateItem(string itemId, Action<T> setupAction)
        {
            Contracts.CheckNonEmpty(itemId, nameof(itemId));
            Contracts.CheckValue(setupAction, nameof(setupAction));

            lock (this._lockObject)
            {
                bool existed = this._items.TryGetValue(itemId, out var item);
                if (!existed)
                {
                    item = this.InstantiateItem(itemId);
                }

                setupAction(item);

                if (!existed)
                {
                    this._items.Add(itemId, item);
                    this.UpdateSignal();
                }

                return item;
            }
        }

        /// <inheritdoc/>
        public IList<T> GetItems()
        {
            lock (this._lockObject)
            {
                return this._items.Values.ToList();
            }
        }

        /// <inheritdoc/>
        public bool TryRemoveItem(string itemId)
        {
            Contracts.CheckNonEmpty(itemId, nameof(itemId));

            lock (this._lockObject)
            {
                bool removed = this._items.Remove(itemId);

                if (removed)
                {
                    this.UpdateSignal();
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
            this._signal.Value = this._items.Select(kvp => kvp.Value).ToList().AsReadOnly();
        }
    }
}
