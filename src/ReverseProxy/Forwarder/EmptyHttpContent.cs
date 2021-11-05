// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Forwarder;

internal sealed class EmptyHttpContent : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

#if NET
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => Task.CompletedTask;
#endif

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return true;
    }
}
