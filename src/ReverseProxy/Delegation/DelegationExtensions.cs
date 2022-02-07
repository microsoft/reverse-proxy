#if NET6_0_OR_GREATER
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Delegation;

internal static class DelegationExtensions
{
    public const string HttpSysQueueNameMetadataKey = "HttpSysQueueName";

    public static string? GetHttpSysQueueName(this DestinationConfig destinationConfig)
    {
        return destinationConfig?.Metadata?.TryGetValue(HttpSysQueueNameMetadataKey, out var name) ?? false
            ? name
            : null;
    }

    public static string? GetHttpSysQueueName(this DestinationState destination)
    {
        return destination?.Model?.Config?.GetHttpSysQueueName();
    }

    public static bool ShouldUseHttpSysQueueDelegation(this DestinationConfig destination)
    {
        return destination.GetHttpSysQueueName() != null;
    }

    public static bool ShouldUseHttpSysQueueDelegation(this DestinationState destination)
    {
        return destination.GetHttpSysQueueName() != null;
    }
}
#endif
