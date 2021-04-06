// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    internal record CustomHeaderSessionAffinitySettings
    {
        internal string CustomHeaderName { get; init; }
    }
}
