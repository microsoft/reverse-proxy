// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Delegation;

internal static class DelegationExtensions
{
    public const string HttpSysDelegationQueueMetadataKey = "HttpSysDelegationQueue";

    public static string? GetHttpSysDelegationQueue(this DestinationState? destination)
    {
        return destination?.Model?.Config?.Metadata?.TryGetValue(HttpSysDelegationQueueMetadataKey, out var name) ?? false
            ? name
            : null;
    }

    public static bool ShouldUseHttpSysDelegation(this DestinationState destination)
    {
        return destination.GetHttpSysDelegationQueue() is not null;
    }
}
