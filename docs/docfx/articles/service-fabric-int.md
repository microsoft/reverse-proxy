# Service Fabric Integration
YARP can be integrated with Service Fabric as a reverse proxy managing HTTP/HTTPS traffic ingress to a Service Fabric cluster, including support for gRPC and Web Sockets. Currently, the integration module is shipped as a separate package and has a limited support of SF availability and scalability scenarios, but it will be gradually improved over time to support more advanced SF deployment schemes.

## Key YARP integration features
- Reverse proxy supporting HTTP/2, gRPC and WebSockets
- Advanced routing in SF cluster
- Sophisticated load balancing algorithms

## Integration component configuration
The following YARP.ServiceFabric parameters can be set in the configuration:
- `ReportReplicasHealth` - indicates whether SF replica health report should be generated after the SF replica to YARP model conversion completed. Default `false`
- `DiscoveryPeriod` - SF cluster topology metadata polling period. Default `00:00:30`
- `AllowStartBeforeDiscovery` - indicates whether YARP can start before the initial SF topology to YARP runtime model conversion completes. Setting it to `true` can lead to having the proxy started with an inconsistent configuration. Default `false`
- `DiscoverInsecureHttpDestinations` - indicates whether to discover unencrypted HTTP (i.e. non-HTTPS) endpoints. Default `false`
- `AlwaysSendImmediateHealthReports` - indicates whether SF service health reports are always sent immediately or only when explicitly requested. This flag controls only 'healthy' reports behavior. 'Unhealthy' ones are always sent immediately. Default `false`

## SF service enlistment
YARP integration is enabled and configured per each SF service. The configuration is specified in the `Service Manifest` as a service extension named `YARP-preview` containing a set of labels defining specific YARP parameters. The parameter's name is set as `Key` attribute of `<Label>` element and the value is the element's content.

These are the supported parameters:
- `YARP.Enable` - indicates whether the service opt-ins to serving traffic through YARP. Default `false`
- `YARP.EnableDynamicOverrides` - indicates whether application parameters replacement is enabled on the service. Default `false`
- `YARP.Routes.<routeName>.Path` - configures path-based route matching
- `YARP.Routes.<routeName>.Host` - configures `Host` header based route matching
- `YARP.Backend.LoadBalancing.Mode` - configures YARP load balancing mode. Optional parameter
- `YARP.Backend.SessionAffinity.*` - configures YARP session affinity. Available parameters and their meanings are provided on [the respective documentation page](xref:session-affinity.md). Optional parameter
- `YARP.Backend.HttpRequest.*` - sets proxied HTTP request properties. Available parameters and their meanings are provided on [the respective documentation page](xref:proxyhttpclientconfig.md) in 'HttpRequest' section. Optional parameter
- `YARP.Backend.HealthCheck.Active.*` - configures YARP active health checks to be run against the given service. Available parameters and their meanings are provided on [the respective documentation page](xref:dests-health-checks.md). Optional parameter
- `YARP.Backend.HealthCheck.Passive.*` - configures YARP passive health checks to be run against the given service. Available parameters and their meanings are provided on [the respective documentation page](xref:dests-health-checks.md). Optional parameter
- `YARP.Backend.Metadata.*` - sets the cluster's metadata. Optional parameter
- `YARP.Backend.BackendId` - overrides the cluster's Id. Default cluster's Id is the SF service name. Optional parameter
- `YARP.Backend.ServiceFabric.ListenerName` - sets an explicit listener name controlling selection of the main service's endpoint for each replica/instance that is used to route client requests to and is stored on the `Destination.Address` property in YARP's model. Optional parameter
- `YARP.Backend.HealthCheck.Active.ServiceFabric.ListenerName` - sets an explicit listener name controlling selection of the health probing endpoint for each replica/instance that is used to probe replica/instance health state and is stored on the `Destination.Health` property in YARP's model. Optional parameter
- `YARP.Backend.ServiceFabric.StatefulReplicaSelectionMode` - sets statefull replica selection mode. Supported values `All`, `PrimaryOnly`, `SecondaryOnly`. Values `All` and `SecondaryOnly` mean that the active secondary replicas will also be eligible for getting all kinds of client requests including writes. Default value `All`

> NOTE: Label values can use the special syntax `[AppParamName]` to reference an application parameter with the name given within square brackets. This is consistent with Service Fabric conventions, see e.g. [using parameters in Service Fabric](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-how-to-specify-port-number-using-parameters).

### Service extension example
```diff
 <ServiceManifest Name="Service1Pkg" Version="1.0.0" xmlns="http://schemas.microsoft.com/2011/01/fabric">
   <ServiceTypes>
     <StatelessServiceType ServiceTypeName="Service1Type" >
       <Extensions>
+        <Extension Name="YARP-preview">
+          <Labels xmlns="http://schemas.microsoft.com/2015/03/fabact-no-schema">
+            <Label Key="YARP.Enable">true</Label>
+            <Label Key="YARP.Routes.route1.Path">{**catchall}</Label>
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
YARP.ServiceFabric dynamically constructs and updates YARP runtime model based on the SF topology metadata. On start, it connects to SF cluster, enumerates the deployed entities (e.g applications, services, etc.), maps to YARP configuration model, validates and applies it. After the initial configuration has been applied, it starts a backround tasks periodically polling SF cluster for topology changes. The polling frequency can be set in the YARP configuration. The Service Fabric to YARP entities mapping is shown in the following table.

Service Fabric | YARP 
:--- | :---
Cluster | n/a
Application | n/a
Service Type | n/a
Named Service Instance | Cluster (ClusterId=ServiceName)
Partition | *TBD later*
Replica / Instance (one endpoint only) | Destination (Address=instance' or replica's endpoint)
YARP.Routes.<routeName>.* in ServiceManifest | ProxyRoute (id=ServiceName+routeName, Match=Hosts,Path extracted from the labels)

## Known limitations
Limitations of the current Service Fabric to YARP configuration model conversion implementation:
- Partitioning is not supported. Partitions are enumerated to retrieve all nested replicas/instances, but partioning key is not handled in any way. Specifically, depending of how YARP routing is configured, it's possible to route a request having one partion key (e.g 'A') to a replica of another partition (e.g. 'B').
- Only one endpoint per each SF service's replica/instance is considered and gets converted to a [Destination](xref:Microsoft.ReverseProxy.Abstractions.Destination).
- All statefull service replica roles are treated equally. No special differentiation logic is applied. Depending on the configuration, active secondary replicas can also be converted to `Destinations` and directly receive client requests.
- Each of named service instance get converted to separate YARP's [Clusters](xref:Microsoft.ReverseProxy.Abstractions.Cluster) which are completely unrelated to each other.
- Naive error handling. It ignores most of the failures.

## Architecture
The detailed process of SF cluster polling and model conversion looks as follows.

[ServiceFabricConfigProvider](xref:Microsoft.ReverseProxy.ServiceFabric.ServiceFabricConfigProvider) invokes [IDiscoverer](xref:Microsoft.ReverseProxy.ServiceFabric.IDiscoverer) to discover the current Service Fabric topology, retrieve its metadata and convert everything to YARP configuration model. `IDiscoverer` calls [IServiceFabricCaller](xref:Microsoft.ReverseProxy.ServiceFabric.IServiceFabricCaller) to fetch the following necessary SF entities from the connected SF cluster: Applications, Services, Partitions, Replicas. Basically, `IServiceFabricCaller` forwards all calls to the Service Fabric API, but it also adds a level of resiliency by caching data returned from succesfull calls which later can be served from the cache should subsequent fetches of the same entities fail. retrieved SF entities gets converted to YARP configuration model as explained futher, but it's worth noting there are limitations in SF topology variants the current YARP.ServiceFabric implementation supports which are listed in the section `Known Limitations`.

Service Fabric to YARP model conversions start with enumerating all SF applications. For each application `IDiscoverer` retrieves all services and calls [IServiceExtensionLabelsProvider](xref:Microsoft.ReverseProxy.ServiceFabric.IServiceExtensionLabelsProvider) to fetch all extension's labels as they are defined by service manifests. `IServiceExtensionLabelsProvider` also replaces application parameter references in labels' values with their actual values if it is enabled for the service by the `YARP.EnableDynamicOverrides` label. At the next step, `IDiscoverer` filters services with enabled YARP integration (`YARP.Enable` labels) and calls `LabelsParser` to build YARP's `Clusters`. Then, it proceeds to `Destinations` construction that starts with fetching partitions and replicas for each SF service. Partitions are handled as simple replica/instance containers and they don't get converted to any YARP model entities. Replicas and instances are mapped to `Destinations`, but only ones in `Ready` state are considered. Additionally, stateful service replicas selection process can be controlled by defining `YARP.Backend.ServiceFabric.StatefulReplicaSelectionMode` label specifying the roles of replicas to be converted. By default, all `Primary` and `Active Secondary` replicas are included. SF replicas can expose several endpoints for client requests and health probes, however `IDiscoverer` picks only one of each type to set `Destination`'s `Address` and `Health` properties. This selection is controlled by defining the respective listener names on the two labels `YARP.Backend.ServiceFabric.ListenerName` and `YARP.Backend.HealthCheck.Active.ServiceFabric.ListenerName`. In case of any error is encountered in the conversion process, the given replica gets skipped, but all remaining replicas of the same partition will be considered. This error handling logic seems quite relaxed and might be tighten in the future. As the last step of a replica conversion, `IDiscoverer` sends a replica health report to SF cluster. The given replica is reported as 'healthy' if the conversion completed successfully, and as 'unhealthy' otherwise.

Once `Cluster` and all its `Destinations` have been built, `IDiscoverer` calls `IConfigValidator` to ensure validity of the produced YARP configuration. The validation is performed incrementally on each new `Cluster` before starting building the next one. In case of validation errors or other conversion failures, the `Cluster`'s construction gets aborted and a health report gets sent to SF cluster indicating the respective service as 'unhealthy'. A failure in conversion of one SF service doesn't fail the whole process, so all remaining services in the given SF application will be properly processed.

If the `Cluster` has been successfully validated, `IDiscoverer` proceeds to the last conversion step where it calls `LabelsParser` to create [ProxyRoutes](xref:Microsoft.ReverseProxy.Abstractions.ProxyRoutes) from `YARP.Routes.*` extension labels. Constructed `ProxyRoutes` are also passed down to `IConfigValidator` to ensure their validity. A failure in `ProxyRoute` construction is communicated to SF cluster in form of a service health report similar to other service conversion failures.

After all applications and their services have been processed and a new full YARP configuration has been constructed, `ServiceFabricConfigProvier` finally updates [IProxyConfig](xref:Microsoft.ReverseProxy.Service.IProxyConfig).

The above process is regularly repeated with the period defined by `DiscoveryPeriod` parameter.

The data flow is shown on the following diagram:
```
    ServiceFabricConfigProvider
                |
    (periodically requests SF topology discovery)
                |
                V
        IDiscoverer <--(enumerate all Applications / Services / Partitions / Replicas)--> IServiceFabricCaller <--> FabricClient
                |
                V
        IDiscoverer (filter YARP-enabled services)
                |
                V
        IDiscoverer (convert SF metadata and YARP extension configs into new YARP configuration) --(validate new configuration)--> IConfigValidator
                |
        IDiscoverer --(report 'unhealthy' states for services and replicas with config validation errors)--> IServiceFabricCaller --> FabricClient
                |
    (update YARP configuration)
                |
                V
    ServiceFabricConfigProvider
                |
    (notify YARP configuration changed)
                |
                V
        IProxyConfigManager
```