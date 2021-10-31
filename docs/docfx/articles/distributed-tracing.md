
# Distributed tracing

As an ASP.NET Core component, YARP can easily integrate into different tracing systems the same as any other web application.
See detailed guides for setting up your application with:
- [OpenTelemetry] or
- [Application Insights]

.NET 6.0 has built-in configurable support for distributed tracing that YARP takes advantage of to enable such scenarios out-of-the-box.

## .NET 5.0 and older

Before 6.0, `SocketsHttpHandler` could not be used with distributed tracing.
When running on .NET 3.1 or 5.0, YARP will copy tracing headers as-is, not accounting for any changes that may have occurred to the trace within the application.

To get YARP to actively participate, you must use a workaround to manually insert the correct headers.

The recommended workaround is to:
- Include a [custom `IForwarderHttpClientFactory`][workaround] in your project and
- Register it in the DI container
    ```c#
    #if !NET6_0_OR_GREATER
    services.AddSingleton<IForwarderHttpClientFactory, DiagnosticsHandlerFactory>();
    #endif
    ```
The workaround mimics the behavior of the internal `DiagnosticsHandler` class used by `HttpClient`. As such, it automatically works with instrumentation packages from OpenTelemetry or Application Insights.

[OpenTelemetry]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/getting-started/README.md
[Application Insights]: https://docs.microsoft.com/en-us/azure/azure-monitor/app/asp-net-core
[workaround]: https://gist.github.com/MihaZupan/dc5de86f7d412e89844480dade65bb43