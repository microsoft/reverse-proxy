#ReverseProxy.Metrics.Sample

This sample demonstrates how to use the ReverseProxy.Telemetry.Consumption library to listen to telemetry data from YARP. In this case it uses the events to create a per-request data structure with detailed timings for each operation that takes place as part of the proxy operation.

Internally YARP uses EventSource to collect telemetry events and metrics from a number of subsystems that are used to process the requests. The YARP telemetry library provides wrapper classes that collect these events metrics and make them available for Consumption. To listen for the metrics you register classes with DI that implement an interface for each subsystem. Event/metric listeners will only be created for the subsystems that you register for, as each registration has performance implications.

The subsystems are:
- **Proxy** which represents the overall proxy operation, and success or failure. 

  Events include:
    - When the proxy request is started and stopped
    - When the request/response bodies have been processed

  Metrics include:
    - Number of requests started
    - Number of request in flight
    - Number of requests that have failed

- **Kestrel** which is the web server that handles incomming requests. 

  Events include:
    - When a request is started and stopped
    
  Metrics include:
    - Connection Rate - how many connections are opened a second
    - Total number of connections
    - Number of TLS handshakes
    - Incomming queue length

- **Http** which is the HttpClient which makes outgoing requests to the destination servers. 

  Events include:
    - When Http connections are created
    - When Http requests are queued and dequeued due to a lack of available connections
    - When Headers are processed
    - When Content starts being transferred. Stop events are not provided for content as it is async and completion is dependent on the consuming code.

  Metrics include:
    - Number of outgoing requests started
    - Number of requests failed
    - Number of active requests
    - Number of outbound connections

- **Sockets** which collects metrics about the amount of data sent and received
- **NameResolution** which collects metrics for DNS lookup of destinations

## Key Files

The following files are key to implementing the features described above:

### Startup.cs

Performs registrtion of the proxy, the listener classes and a custom ASP.NET middleware step that starts per-request telemetry and reports the results when complete

### ProxyTelemetryConsumer.cs

Listens to events from the proxy telemetry and records timings and info about the high level processing involved in proxying a request.

### HttpTelemetryConsumer.cs

Listens to events from the HttpClient telemetry and records timings and info about the outbound request and response from the destination server.

### PerRequestMetrics.cs

Class to store the metrics on a per request basis. Instances are stored in AsyncLocal storage for the duration of the request. 

### PerRequestYarpMetricCollectionMiddleware.cs

ASP.NET Core middleware that is the first and last thing called as part of the ASP.NET handling of the request. It initializes the per-request metrics and logs the results at the end of the request.
