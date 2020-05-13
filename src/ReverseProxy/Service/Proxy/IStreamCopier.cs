// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Service.Proxy
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
