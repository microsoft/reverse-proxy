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
        private readonly object lockObject = new object();
        private readonly Dictionary<string, T> items = new Dictionary<string, T>(StringComparer.Ordinal);
        private readonly Signal<IReadOnlyList<T>> signal = SignalFactory.Default.CreateSignal<IReadOnlyList<T>>(new List<T>().AsReadOnly());

        /// <inheritdoc/>
        public IReadableSignal<IReadOnlyList<T>> Items => this.signal;

        /// <inheritdoc/>
        public T TryGetItem(string itemId)
        {
            Contracts.CheckNonEmpty(itemId, nameof(itemId));

            lock (this.lockObject)
            {
                this.items.TryGetValue(itemId, out var item);
                return item;
            }
        }

        /// <inheritdoc/>
        public T GetOrCreateItem(string itemId, Action<T> setupAction)
        {
            Contracts.CheckNonEmpty(itemId, nameof(itemId));
            Contracts.CheckValue(setupAction, nameof(setupAction));

            lock (this.lockObject)
            {
                bool existed = this.items.TryGetValue(itemId, out var item);
                if (!existed)
                {
                    item = this.InstantiateItem(itemId);
                }

                setupAction(item);

                if (!existed)
                {
                    this.items.Add(itemId, item);
                    this.UpdateSignal();
                }

                return item;
            }
        }

        /// <inheritdoc/>
        public IList<T> GetItems()
        {
            lock (this.lockObject)
            {
                return this.items.Values.ToList();
            }
        }

        /// <inheritdoc/>
        public bool TryRemoveItem(string itemId)
        {
            Contracts.CheckNonEmpty(itemId, nameof(itemId));

            lock (this.lockObject)
            {
                bool removed = this.items.Remove(itemId);

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
            this.signal.Value = this.items.Select(kvp => kvp.Value).ToList().AsReadOnly();
        }
    }
}
