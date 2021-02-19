// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// For use with <see cref="RequestHeaderForwardedTransform"/>.
    /// </summary>
    public enum NodeFormat
    {
        None,
        Random,
        RandomAndPort,
        RandomAndRandomPort,
        Unknown,
        UnknownAndPort,
        UnknownAndRandomPort,
        Ip,
        IpAndPort,
        IpAndRandomPort,
    }
}
