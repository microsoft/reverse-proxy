// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Delegation;

internal static class DelegationExtensions
{
    public const string HttpSysDelegationQueueMetadataKey = "HttpSysDelegationQueue";

    public static string? GetHttpSysDelegationQueue(this DestinationConfig destinationConfig)
    {
        return destinationConfig?.Metadata?.TryGetValue(HttpSysDelegationQueueMetadataKey, out var name) ?? false
            ? name
            : null;
    }

    public static string? GetHttpSysDelegationQueue(this DestinationState destination)
    {
        return destination?.Model?.Config?.GetHttpSysDelegationQueue();
    }

    public static bool ShouldUseHttpSysDelegation(this DestinationConfig destination)
    {
        return destination.GetHttpSysDelegationQueue() != null;
    }

    public static bool ShouldUseHttpSysDelegation(this DestinationState destination)
    {
        return destination.GetHttpSysDelegationQueue() != null;
    }
}
#endif
