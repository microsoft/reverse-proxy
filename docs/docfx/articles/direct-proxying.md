---
uid: direct-proxying
title: Direct Proxying
---

# Direct Proxying

Introduced: preview6

Some applications only need the ability to take a specific request and proxy it to a specific destination. These applications do not need, or have addressed in other ways, the other features of the proxy like configuration discovery, routing, load balancing, etc..

## IHttpProxy

[IHttpProxy](xref:Microsoft.ReverseProxy.Service.Proxy.IHttpProxy) serves as the core proxy adapter between incoming AspNetCore and outgoing System.Net.Http requests. It handles the mechanics of creating a HttpRequestMessage from a HttpContext, sending it, and relaying the response.

IHttpProxy supports:
- Dynamic destination selection, you specify the destination for each request
- Http client customization, you provide the HttpMessageInvoker
- Request and response customization (except bodies)
- Streaming protocols like gRPC and WebSockets
- Error handling

It does not include:
- Routing
- Load balancing
- Affinity
- Retries

## Example

See [ReverseProxy.Direct.Sample](https://github.com/microsoft/reverse-proxy/tree/main/samples/ReverseProxy.Direct.Sample) as a pre-built sample, or use the steps below.

### Create a new project

Follow the [Getting Started](xref:getting_started) guide to create a project and add the Microsoft.ReverseProxy nuget dependency.

### Update Startup

In this example the IHttpProxy is registered in DI, injected into the `Startup.Configure` method, and used to proxy requests from a specific route to `https://localhost:10000/prefix/`.

The optional transforms show how to copy all request headers except for the `Host`, it's common that the destination requires its own `Host` from the url.

```C#
public void ConfigureServices(IServiceCollection services)
{
    services.AddHttpProxy();
}

public void Configure(IApplicationBuilder app, IHttpProxy httpProxy)
{
    var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false
    });
    var transformer = new CustomTransformer(); // or HttpTransformer.Default;
    var requestOptions = new RequestProxyOptions { Timeout = TimeSpan.FromSeconds(100) };

    app.UseRouting();
    app.UseAuthorization();
    app.UseEndpoints(endpoints =>
    {
        endpoints.Map("/{**catch-all}", async httpContext =>
        {
            await httpProxy.ProxyAsync(httpContext, "https://localhost:10000/", httpClient,
                requestOptions, transformer);

            var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
            if (errorFeature != null)
            {
                var error = errorFeature.Error;
                var exception = errorFeature.Exception;
            }
        });
    });
}
```
C#
```
private class CustomTransformer : HttpTransformer
{
    public override async Task TransformRequestAsync(HttpContext httpContext,
        HttpRequestMessage proxyRequest, string destinationPrefix)
    {
        // Copy headers normally and then remove the original host.
        // Use the destination host from proxyRequest.RequestUri instead.
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);
        proxyRequest.Headers.Host = null;
    }
}
```

### The HTTP Client

The http client may be customized, but the above example is recommended for common proxy scenarios.

Always use HttpMessageInvoker rather than HttpClient, HttpClient buffers responses by default. Buffering breaks streaming scenarios and increases memory usage and latency.

Re-using a client for requests to the same destination is recommended for performance reasons as it allows you to re-use pooled connections. A client may also be re-used for requests to different destinations if the configuration is the same.

### Transforms

The request and response can be modified by providing a derived [HttpTransformer](xref:Microsoft.ReverseProxy.Service.Proxy.HttpTransformer) as a parameter to [`ProxyAsync`](xref:Microsoft.ReverseProxy.Service.Proxy.IHttpProxy) method.

### Error handling

IHttpProxy catches exceptions and timeouts from the HTTP client, logs them, and converts them to 5xx status codes or aborts the response. The error details, if any, can be accessed from the [IProxyErrorFeature](xref:Microsoft.ReverseProxy.Service.Proxy.IProxyErrorFeature) as shown above.
