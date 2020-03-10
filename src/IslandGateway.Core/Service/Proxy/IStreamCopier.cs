// <copyright file="IStreamCopier.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IslandGateway.Core.Service.Proxy
{
    /// <summary>
    /// Provides a method to copy the contents of one stream to another stream.
    /// </summary>
    internal interface IStreamCopier
    {
        /// <summary>
        /// Copies the <paramref name="source"/> stream into the <paramref name="destination"/> stream.
        /// </summary>
        Task CopyAsync(Stream source, Stream destination, CancellationToken cancellation);
    }
}