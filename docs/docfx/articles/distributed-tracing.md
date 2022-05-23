
# Distributed tracing

As an ASP.NET Core component, YARP can easily integrate into different tracing systems the same as any other web application.
See detailed guides for setting up your application with:
- [OpenTelemetry] or
- [Application Insights]

.NET has built-in configurable support for distributed tracing that YARP takes advantage of to enable such scenarios out-of-the-box.

## Using custom tracing headers

When using a propagation mechanism that is not built into .NET (e.g. [B3 propagation]), you should implement a custom [`DistributedContextPropagator`] for that scheme.

YARP will remove any header in [`DistributedContextPropagator.Fields`] so that the propagator may re-add them to the request during the `Inject` call.

## Pass-through proxy

If you do not wish the proxy to actively participate in the trace, and wish to keep all the tracing headers as-is, you may do so by setting `SocketsHttpHandler.ActivityHeadersPropagator` to `null`.

```c#
services.AddReverseProxy()
    .ConfigureHttpClient((context, handler) => handler.ActivityHeadersPropagator = null);
```

[OpenTelemetry]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/getting-started/README.md
[Application Insights]: https://docs.microsoft.com/azure/azure-monitor/app/asp-net-core
[B3 propagation]: https://github.com/openzipkin/b3-propagation
[`DistributedContextPropagator`]: https://docs.microsoft.com/dotnet/api/system.diagnostics.distributedcontextpropagator
[`DistributedContextPropagator.Fields`]: https://docs.microsoft.com/dotnet/api/system.diagnostics.distributedcontextpropagator.fields
