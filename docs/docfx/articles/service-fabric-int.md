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

There are the following supported parameters:
- `YARP.Enable` - indicates whether the service opt-ins to serving traffic through YARP. Default `false`
- `YARP.Routes.<routeName>.Path` - configures path-based route matching
- `YARP.Routes.<routeName>.Host` - configures `Host` header based route matching
- `YARP.Backend.Healthcheck.Active.*` - configures YARP active health checks to be run against the given service. Available parameters and their meanings are provided on [the respective documentation page](xref:dests-health-checks.md)

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
+            <Label Key='YARP.Backend.Healthcheck.Active.Enabled'>true</Label>
+            <Label Key='YARP.Backend.Healthcheck.Active.Timeout'>30</Label>
+            <Label Key='YARP.Backend.Healthcheck.Active.Interval'>10</Label>
+            <Label Key='YARP.Backend.Healthcheck.Active.Policy'>ConsecutiveFailures</Label>
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
Replica / Instance | Destination (Address=instance' or replica's endpoint)
YARP.Routes.<routeName>.* in ServiceManifest | ProxyRoute (id=ServiceName+routeName, Match=Hosts,Path extracted from the labels)

## Architecture
The overall polling and conversion process is shown on the following diagram:
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