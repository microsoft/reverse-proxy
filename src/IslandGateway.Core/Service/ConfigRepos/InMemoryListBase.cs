// <copyright file="InMemoryListBase.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace IslandGateway.Core.Service
{
    internal abstract class InMemoryListBase<T>
        where T : IDeepCloneable<T>
    {
        private readonly object syncRoot = new object();
        private IList<T> items;

        protected IList<T> Get()
        {
            lock (this.syncRoot)
            {
                return this.items?.DeepClone();
            }
        }

        protected void Set(IList<T> items)
        {
            lock (this.syncRoot)
            {
                this.items = items?.DeepClone();
            }
        }
    }
}
