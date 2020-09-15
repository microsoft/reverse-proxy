// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.Proxy
{
    internal enum StreamCopyResult
    {
        Success,
        InputError,
        OutputError,
        Canceled
    }
}
