# Destination health checks
In most of the real-world systems, it's expected for their nodes to occasionally experience transient issues and go down completely due to a variety of reasons such as an overload, resource leakage, hardware failures, etc. Ideally, it'd be desirable to completely prevent those unfornunate events from occuring in a proactive way, but the cost of desiging and building such an ideal system is generally prohibitively high. However, there is another reactive approach which is cheaper and aimed to minimizing a negative impact failures cause on client requests by constantly analyzing nodes health and stopping sending client traffic to ones became unhealthy until they have recovered. YARP implements this apporach in the form of active and passive destination health checks.

## Active health checks
YARP proactively monitors destination health by sending periodic probing requests to designated health endpoints and analyzing responses. The main service in this process is [IActiveHealthMonitor](xref:Microsoft.ReverseProxy.Service.HealthChecks.IActiveHealthMonitor) that periodically creates probing requests via [IProbingRequestFactory](xref:Microsoft.ReverseProxy.Service.HealthChecks.IProbingRequestFactory), sends them to all [Destinations](xref:Microsoft.ReverseProxy.Abstractions.Destination) of each [Cluster](xref:Microsoft.ReverseProxy.Abstractions.Cluster) with enabled active health checks and then passes all the responses down to a [IActiveHealthCheckPolicy](xref:Microsoft.ReverseProxy.Service.HealthChecks.IActiveHealthCheckPolicy) specified for a cluster. IActiveHealthMonitor doesn't make the actual decision on whether a destination is healthy or not, but delegates this duty to an IActiveHealthCheckPolicy specified for a cluster. A policy is called to evaluate the new health states once all probing of all cluster's destination completed. It takes in a [ClusterInfo](xref:Microsoft.ReverseProxy.RuntimeModel.ClusterInfo) representing the cluster's dynamic state and a set of [DestinationProbingResult](xref:Microsoft.ReverseProxy.Service.HealthChecks.DestinationProbingResult) storing cluster's destinations' probing results. Having evaluated a new health state for each destination, the policy actually updates [CompositeDestinationHealth.Active](xref:Microsoft.ReverseProxy.RuntimeModel.CompositeDestinationHealth.Active) value.

There are default built-in implementation for all of the aforementioned components which can also be replaced with custom ones when necessary.

### Built-in policies
There is one built-in active health check policy - `ConsecutiveFailuresHealthPolicy`. It counts consecutive health probe failures and marks a destination as unhealthy once the given threshold is reached. On the first successful response, a destination is marked as healthy and the counter is reset.
The policy parameters are set in the cluster's metadata as follows:

`ConsecutiveFailuresHealthPolicy.Threshold` - number of consecutively failed active health probing requests required to mark a destination as unhealthy. Default `2`.

### Configuration
All but one of active health check settings are specified on the cluster level in `Cluster/HealthCheck` section. The only exception is an optional `Destination/Health` element specifying a separate active health check endpoint. The actual health probing URI is constructed as `Destination/Address` (or `Destination/Health` when it's set) + `Cluster/HealthCheck/Path`.

`Cluster/HealthCheck` section:

- `Enabled` - flag indicating whether active health check is enabled for a cluster. Default `false`
- `Interval` - period of sending health probing requests. Default `00:00:15`
- `Timeout` - probing request timeout. Default `00:00:10`
- `Policy` - name of a policy evaluating destinations' active health states. Mandatory parameter
- `Path` -  health check path on all cluster's destinations. Default `null`.

`Destination` section.

- `Health` - dedicated health probing endpoint. Default `null`.

#### Example
```JSON
"Clusters": {
      "cluster1": {
        "LoadBalancing": {
          "Mode": "Random"
        },
        "HealthCheck": {
          "Active": {
            "Enabled": "true",
            "Interval": "00:00:10",
            "Timeout": "00:00:10",
            "Policy": "ConsecutiveFailures",
            "Path": "/api/health"
          }
        },
        "Metadata": {
          "ConsecutiveFailuresHealthPolicy.Threshold": "3"
        },
        "Destinations": {
          "cluster1/destination1": {
            "Address": "https://localhost:10000/"
          },
          "cluster1/destination2": {
            "Address": "http://localhost:10010/",
            "Health": "http://localhost:10020/"
          }
        }
      }
```

### Code configuration
Active health check configuration can also be specified in code.

#### Example
```C#
var clusters = new[]
{
    new Cluster()
    {
        Id = "cluster1",
        HealthCheck = new HealthCheckOptions
        {
            Active = new ActiveHealthCheckOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(10),
                Policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures,
                Path = "/api/health"
            }
        },
        Metadata = new Dictionary<string, string> { { ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName, "5" } },
        Destinations =
        {
            { "destination1", new Destination() { Address = "https://localhost:10000" } },
            { "destination2", new Destination() { Address = "https://localhost:10010", Health = "https://localhost:10010" } }
        }
    }
};
```
