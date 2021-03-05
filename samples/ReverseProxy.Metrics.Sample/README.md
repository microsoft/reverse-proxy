# YARP Metrics & Telemetry Sample

This sample uses the Microsoft.ReverseProxy.Telemetry.Consumption package to show how to consume metrics and events from YARP. The sample provides listener which implement IProxyMetricsConsumer and IProxyTelemetryConsumer to get callbacks for metrics and significant request events. In this sample they write to the console, but you could use to write to other event and metric collection systems.

The major parts of this sample are:
- ### [Startup.cs](Startup.cs)
  This provides the initalization for YARP and the ASP.NET infrastructure it relies upon. 


- Using IProxyMetricsConsumer and IProxyTelemetryConsumer to get callbacks for metrics and significant request events
- Using ILogger to get log entries for actions performed by YARP and related modules
- Using EventList
