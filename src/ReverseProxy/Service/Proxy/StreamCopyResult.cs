// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Service.Proxy
{
    internal enum StreamCopyResult
    {
        Success,
        InputError,
        OutputError,
        Canceled
    }
}
