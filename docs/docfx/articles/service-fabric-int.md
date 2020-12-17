# Service Fabric Integration

Introduced: preview8

YARP can be integrated with Service Fabric as a reverse proxy managing HTTP/HTTPS traffic ingress to a Service Fabric cluster, including support for gRPC and Web Sockets. Currently, the integration module is shipped as a separate package and has a limited support of SF availability and scalability scenarios, but it will be gradually improved over time to support more advanced SF deployment schemes.

## Key YARP integration features
- Reverse proxy supporting HTTP/2, gRPC and WebSockets
- Advanced routing in SF cluster
- A variety of load balancing algorithms

## Update the project file
Open the Project and find the `ItemGroup` referencing the YARP package, then add a reference to the ServiceFabric package next to it.
 
 ```XML
<ItemGroup> 
  <PackageReference Include="Microsoft.ReverseProxy" Version="1.0.0-preview.8.*" />
  <PackageReference Include="Microsoft.ReverseProxy.ServiceFabric" Version="1.0.0-preview.8.*" />
</ItemGroup> 
```

## Update Startup
SF integration is plugged into the rest of YARP via a special `IProxyConfigProvider` and various support service. All of that can be registered in the Asp.Net Core DI container with the following code:
```C#
public void ConfigureServices(IServiceCollection services)
{
    services.AddControllers();
    services.AddReverseProxy()
        .LoadFromServiceFabric(_configuration.GetSection("ServiceFabricDiscovery"));
}

public void Configure(IApplicationBuilder app)
{
    app.UseRouting();
    app.UseAuthorization();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
        endpoints.MapReverseProxy();
    });
}
```

## Integration component configuration
The following YARP.ServiceFabric parameters can be set in the configuration section `ServiceFabricDiscovery`:
- `ReportReplicasHealth` - indicates whether SF replica health report should be generated after the SF replica to YARP model conversion completed. Default `false`
- `DiscoveryPeriod` - SF cluster topology metadata polling period. Default `00:00:30`
- `AllowStartBeforeDiscovery` - indicates whether YARP can start before the initial SF topology to YARP runtime model conversion completes. Setting it to `true` can lead to having the proxy started with an inconsistent configuration. Default `false`
- `DiscoverInsecureHttpDestinations` - indicates whether to discover unencrypted HTTP (i.e. non-HTTPS) endpoints. Default `false`
- `AlwaysSendImmediateHealthReports` - indicates whether SF service health reports are always sent immediately or only when explicitly requested. This flag controls only 'healthy' reports behavior. 'Unhealthy' ones are always sent immediately. Default `false`

### Config example
The following is an example of an `appsettings.json` file with `ServiceFabricDiscovery` section.
```JSON
{
  "ServiceFabricDiscovery": {
    "DiscoveryPeriod": "00:00:05",
    "DiscoverInsecureHttpDestinations": true,
    "AlwaysSendImmediateHealthReports": true
  }
}
```
It can be loaded into the ServiceFabricDiscoveryOptions at startup with the following code with passing configuration section to `LoadFromServiceFabric` method:
```C#
services.AddReverseProxy()
        .LoadFromServiceFabric(_configuration.GetSection("ServiceFabricDiscovery"));
```
Or it can be configured from code:
```C#
services.AddReverseProxy()
        .LoadFromServiceFabric(options =>
        {
            options.DiscoveryPeriod = TimeSpan.FromSeconds(5);
            options.DiscoverInsecureHttpDestinations = true;
            options.AlwaysSendImmediateHealthReports = true;
        });
```

## SF service enlistment
YARP integration is enabled and configured per each SF service. The configuration is specified in the `Service Manifest` as a service extension named `YARP-preview` containing a set of labels defining specific YARP parameters. The parameter's name is set as `Key` attribute of `<Label>` element and the value is the element's content.

These are the supported parameters:
- `YARP.Enable` - indicates whether the service opt-ins to serving traffic through YARP. Default `false`
- `YARP.EnableDynamicOverrides` - indicates whether application parameters replacement is enabled on the service. Default `false`
- `YARP.Backend.LoadBalancingPolicy` - configures YARP load balancing policy. Optional parameter
- `YARP.Backend.SessionAffinity.*` - configures YARP session affinity. Available parameters and their meanings are provided on [the respective documentation page](session-affinity.md). Optional parameter
- `YARP.Backend.HttpRequest.*` - sets proxied HTTP request properties. Available parameters and their meanings are provided on [the respective documentation page](proxyhttpclientconfig.md) in 'HttpRequest' section. Optional parameter
- `YARP.Backend.HealthCheck.Active.*` - configures YARP active health checks to be run against the given service. Available parameters and their meanings are provided on [the respective documentation page](dests-health-checks.md). There is one label in this group `YARP.Backend.HealthCheck.Active.ServiceFabric.ListenerName` which is not covered by that document because it's SF specific. Its purpose is explained below. Optional parameter
- `YARP.Backend.HealthCheck.Active.ServiceFabric.ListenerName` - sets an explicit listener name controlling selection of the health probing endpoint for each replica/instance that is used to probe replica/instance health state and is stored on the `Destination.Health` property in YARP's model. Optional parameter
- `YARP.Backend.HealthCheck.Passive.*` - configures YARP passive health checks to be run against the given service. Available parameters and their meanings are provided on [the respective documentation page](dests-health-checks.md). Optional parameter
- `YARP.Backend.Metadata.*` - sets the cluster's metadata. Optional parameter
- `YARP.Backend.BackendId` - overrides the cluster's Id. Default cluster's Id is the SF service name. Optional parameter
- `YARP.Backend.ServiceFabric.ListenerName` - sets an explicit listener name controlling selection of the main service's endpoint for each replica/instance that is used to route client requests to and is stored on the `Destination.Address` property in YARP's model. Optional parameter
- `YARP.Backend.ServiceFabric.StatefulReplicaSelectionMode` - sets statefull replica selection mode. Supported values `All`, `PrimaryOnly`, `SecondaryOnly`. Values `All` and `SecondaryOnly` mean that the active secondary replicas will also be eligible for getting all kinds of client requests including writes. Default value `All`

> NOTE: Label values can use the special syntax `[AppParamName]` to reference an application parameter with the name given within square brackets. This is consistent with Service Fabric conventions, see e.g. [using parameters in Service Fabric](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-how-to-specify-port-number-using-parameters).

### Route definitions
Multiple routes can be defined in an SF service configuration with the following parameters:
- `YARP.Routes.<routeName>.Path` - configures path-based route matching. The value directly assigned to [ProxyMatch.Path](xref:Microsoft.ReverseProxy.Abstractions.ProxyMatch) property and the standard route matching logic will be applied. `{**catch-all}` path may be used to route all requests.
- `YARP.Routes.<routeName>.Host` - configures `Host` header based route matching. Multiple hosts should be separated by comma `,`. The value is split into a list of host names which is then directly assigned to [ProxyMatch.Hosts](xref:Microsoft.ReverseProxy.Abstractions.ProxyMatch) property and the standard route matching logic will be applied.
- `<routeName>` can contain an ASCII letter, a number, or '_' and '-' symbols.

Each route requires a `Path` or `Host` (or both). If both of them are specified, then a request is matched to the route only when both of them are matched.

Example:
```XML
<Label Key="YARP.Routes.route-A1.Path">/api</Label>
<Label Key="YARP.Routes.route-B2.Path">/control-api</Label>
<Label Key="YARP.Routes.route-B2.Host">example.com,anotherexample.com</Label>
```

### Service extension example
```diff
 <ServiceManifest Name="Service1Pkg" Version="1.0.0" xmlns="http://schemas.microsoft.com/2011/01/fabric">
   <ServiceTypes>
     <StatelessServiceType ServiceTypeName="Service1Type" >
       <Extensions>
+        <Extension Name="YARP-preview">
+          <Labels xmlns="http://schemas.microsoft.com/2015/03/fabact-no-schema">
+            <Label Key="YARP.Enable">true</Label>
+            <Label Key="YARP.Routes.route1.Path">{**catch-all}</Label>
+            <Label Key='YARP.Backend.HealthCheck.Active.Enabled'>true</Label>
+            <Label Key='YARP.Backend.HealthCheck.Active.Timeout'>30</Label>
+            <Label Key='YARP.Backend.HealthCheck.Active.Interval'>10</Label>
+            <Label Key='YARP.Backend.HealthCheck.Active.Policy'>ConsecutiveFailures</Label>
+          </Labels>
+        </Extension>
       </Extensions>
     </StatelessServiceType>
   </ServiceTypes>
 
   <!-- ... -->
 </ServiceManifest>
```

## Dynamic YARP model
YARP.ServiceFabric dynamically constructs and updates the YARP runtime model based on the SF topology metadata. On start, it connects to the SF cluster it's running in, enumerates the deployed entities (e.g applications, services, etc.), maps to the YARP configuration model, validates and applies it. After the initial configuration has been applied, YARP starts a background tasks periodically polling the SF cluster for topology changes. The polling frequency can be set in the YARP configuration. The Service Fabric to YARP entities mapping is shown in the following table.

Service Fabric | YARP 
:--- | :---
Cluster | n/a
Application | n/a
Service Type | n/a
Named Service Instance | Cluster (ClusterId=ServiceName)
Partition | *TBD later*
Replica / Instance (one endpoint only) | Destination (Address=instance' or replica's endpoint)
YARP.Routes.<routeName>.* in ServiceManifest | ProxyRoute (id=ServiceName+routeName, Match=Hosts,Path extracted from the labels)

## Testing SF integration locally
While developing a new YARP-based application with enabled SF integration, it's helpful to test how everything works locally on a dev machine before deploying it to the cloud. This can be done by following the steps explained in [Prepare your development environment on Windows](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-get-started) guide.

There is also the [step-by-step guide](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-tutorial-create-dotnet-app#create-an-aspnet-web-api-service-as-a-reliable-service) on how to create a sample SF service and deploy it to the local SF cluster. Once the sample SF project is created, the 'YARP-preview' section shown in the 'Service extension example' above must be added into `ServiceManifest.xml` to enable YARP.ServiceFabric.

## Known limitations
Limitations of the current Service Fabric to YARP configuration model conversion implementation:
- Partitioning is not supported. Partitions are enumerated to retrieve all of the nested replicas/instances, but the partitioning key is not handled in any way. Specifically, depending on how YARP routing is configured, it's possible to route a request having one partition key (e.g 'A') to a replica of another partition (e.g. 'B').
- Only one endpoint per each SF service's replica/instance is considered and gets converted to a [Destination](xref:Microsoft.ReverseProxy.Abstractions.Destination).
- All statefull service replica roles are treated equally. No special differentiation logic is applied. Depending on the configuration, active secondary replicas can also be converted to `Destinations` and directly receive client requests.
- Each of the named service instances get converted to separate YARP [Clusters](xref:Microsoft.ReverseProxy.Abstractions.Cluster) which are completely unrelated to each other.
- Limited error handling. Most failures are logged and suppressed. Some errors will prevent the proxy config from being updated.

## Architecture
The detailed process of SF cluster polling and model conversion looks as follows.

`ServiceFabricConfigProvider` invokes `IDiscoverer` to discover the current Service Fabric topology, retrieve its metadata and convert everything to YARP configuration model. `IDiscoverer` calls `ICachedServiceFabricCaller` to fetch the following necessary SF entities from the connected SF cluster: Applications, Services, Partitions, Replicas. Basically, `ICachedServiceFabricCaller` forwards all calls to the Service Fabric API, but it also adds a level of resiliency by caching data returned from succesfull calls which later can be served from the cache should subsequent fetches of the same entities fail. Retrieved SF entities get converted to YARP configuration model as explained futher, but it's worth noting that not all of SF topology configurations are supported as it's explained in the section `Known Limitations`.

Service Fabric to YARP model conversion starts by enumerating all SF applications. For each application, `IDiscoverer` retrieves all its services and calls `IServiceExtensionLabelsProvider` to fetch all extension labels defined in the service manifests. `IServiceExtensionLabelsProvider` also replaces application parameters references with their actual values if it is enabled for the service by the `YARP.EnableDynamicOverrides` label. At the next step, `IDiscoverer` filters services with enabled YARP integration (`YARP.Enable` label) and calls `LabelsParser` to build YARP's `Clusters`. Then, it begins building `Destinations` by fetching partitions and replicas for each SF service. Partitions are handled as simple replica/instance containers and they don't get converted to any YARP model entities. Replicas and instances are mapped to `Destinations`, but only those which are in `Ready` state are considered. Additionally, there is a control over which stateful service replicas are converted based on their roles as it's specified by `YARP.Backend.ServiceFabric.StatefulReplicaSelectionMode`. By default, all `Primary` and `Active Secondary` replicas are mapped to `Destinations`. A SF replica can expose several endpoints for client requests and health probes, however `IDiscoverer` picks only one of each type to set `Destination`'s `Address` and `Health` properties respectively. This selection logic is controlled by specifying listener names on the two labels `YARP.Backend.ServiceFabric.ListenerName` (for `Destination.Address` endpoint) and `YARP.Backend.HealthCheck.Active.ServiceFabric.ListenerName` (for `Destination.Health` endpoint). If any error is encountered in the conversion process, the given replica gets skipped, but all remaining replicas of the same partition will be considered. Overall, the error handling logic is now quite relaxed and might be tighten up in the future. As the last step of a replica conversion, `IDiscoverer` sends a replica health report to SF cluster. The replica is reported as 'healthy' if the conversion completed successfully, and as 'unhealthy' otherwise.

Once `Cluster` and all its `Destinations` have been built, `IDiscoverer` calls `IConfigValidator` to check validity of the produced YARP configuration. The validation is performed incrementally on each completed `Cluster` before starting building the next one. In case of validation errors or other service conversion failures, the `Cluster`'s construction gets aborted and a health report gets sent to SF cluster indicating that the respective service is 'unhealthy'. A failure in conversion of one SF service doesn't fail the whole process, so all remaining services in the same SF application will be considered.

If the `Cluster` has been successfully validated, `IDiscoverer` proceeds to the final conversion step where it calls `LabelsParser` to create [ProxyRoutes](xref:Microsoft.ReverseProxy.Abstractions.ProxyRoute) from `YARP.Routes.*` extension labels. New `ProxyRoutes` are also passed down to `IConfigValidator` to ensure their validity. A failure in `ProxyRoute` construction is communicated to SF cluster in form of a service health report similar to other service conversion failures.

After all applications and their services have been processed and a new complete YARP configuration has been constructed, `ServiceFabricConfigProvier` updates [IProxyConfig](xref:Microsoft.ReverseProxy.Service.IProxyConfig) and notifies the rest of YARP about a configuration change.

The above process is regularly repeated with the period set in `DiscoveryPeriod` parameter.

The data flow is shown on the following diagram:
```
    ServiceFabricConfigProvider
                |
    (periodically requests SF topology discovery)
                |
                V
           IDiscoverer <--(enumerate all Applications / Services / Partitions / Replicas)--> ICachedServiceFabricCaller <--(cache response)--> FabricClient
                |
    (filter YARP-enabled services)
                V
           IDiscoverer <--(retrieve extension labels and replace app parameters references)--> IServiceExtensionLabelsProvider <--> ICachedServiceFabricCaller <--(cache response)--> FabricClient
                |
    (convert SF metadata and extension labels into new YARP configuration)
                |
                V
           LabelsParser
                |
    (validate new configuration)
                |
                V
          IConfigValidator --(report 'unhealthy' states for services and replicas with config validation errors)--> ICachedServiceFabricCaller --> FabricClient
                |
    (assemble a complete YARP configuration)
                |
                V
           IDiscoverer --(return new YARP configuration)-->ServiceFabricConfigProvider
                                                                   |
                                        (update IProxyConfig and notify that configuration changed)
                                                                   |
                                                                   V
                                                           IProxyConfigManager
```
