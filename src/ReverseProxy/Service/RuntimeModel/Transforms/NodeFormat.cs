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
        Unknown,
        UnknownAndPort,
        Ip,
        IpAndPort,
    }
}
