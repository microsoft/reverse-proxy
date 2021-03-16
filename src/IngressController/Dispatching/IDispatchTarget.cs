// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace IngressController.Dispatching
{
    /// <summary>
    /// IDispatchTarget is what an <see cref="IDispatcher"/> will use to
    /// dispatch information.
    /// </summary>
    public interface IDispatchTarget
    {
        public Task SendAsync(byte[] utf8Bytes, CancellationToken cancellationToken);
    }
}
