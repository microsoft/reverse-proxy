namespace Yarp.ReverseProxy.Configuration;

public sealed record WeightCluster
{
    public string? ClusterId  { get; init; }
    public int? Weight { get; init; }
}
