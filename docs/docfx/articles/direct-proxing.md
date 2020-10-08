---
uid: direct-proxing
title: Direct Proxying
---

# Direct Proxying

Some applications only need the ability to take a specific request and proxy it to a specific destination. These applications do not need, or have addressed in other ways, the other features of the proxy like configuration discovery, routing, load balancing, etc..

## IHttpProxy

IHttpProxy serves as the core proxy adapter between AspNetCore and HttpClient. It handles the mechanics of creating a HttpRequestMessage from an HttpContext, sending it, and relaying the response.

IHttpProxy supports:
- Dynamic destination selection, you specify the destination for each request.
- Http client customization, you provide the HttpMessageInvoker.
- Streaming protocols like gRPC and WebSockets
- Error handling

It does not include:
- Load balancing
- Routing
- Retries
- Affinity

## Example

### Create a new project

Follow the [Getting Started](xref:getting_started.md) guide to create a project and add the Microsoft.ReverseProxy nuget dependency.

### Update Startup

In this example the IHttpProxy is registered in DI, injected into the `Startup.Configure` method, and used to proxy requests from a specific route to `https://localhost:10000/`.

The optional transforms show how to copy all request headers except for the `Host` as the destination may require its own `Host` from the url.

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
    var proxyOptions = new RequestProxyOptions()
    {
        RequestTimeout = TimeSpan.FromSeconds(100),
        // Copy all request headers except Host
        Transforms = new Transforms(
            copyRequestHeaders: true,
            requestTransforms: Array.Empty<RequestParametersTransform>(),
            requestHeaderTransforms: new Dictionary<string, RequestHeaderTransform>()
            {
                { HeaderNames.Host, new RequestHeaderValueTransform(string.Empty, append: false) }
            },
            responseHeaderTransforms: new Dictionary<string, ResponseHeaderTransform>(),
            responseTrailerTransforms: new Dictionary<string, ResponseHeaderTransform>())
    };

    app.UseRouting();
    app.UseAuthorization();
    app.UseEndpoints(endpoints =>
    {
        await httpProxy.ProxyAsync(httpContext, "https://localhost:10000/", httpClient, proxyOptions);
        var errorFeature = httpContext.Features.Get<IProxyErrorFeature>();
        if (errorFeature != null)
        {
            var error = errorFeature.Error;
            var exception = errorFeature.Exception;
        }
    });
}
```

### The HTTP Client

The http client may be customized, but the following example is recommended for common proxy scenarios:

```C#
    var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false
    });
```

Always use HttpMessageInvoker rather than HttpClient. See https://github.com/microsoft/reverse-proxy/issues/458 for details.

Re-using a client for requests to the same destination is recommended for performance reasons as it allows you to re-use pooled connections. A client may also be re-used for requests to different destinations if the configuration is the same.

### Transforms

The request and response can be modified by providing [transforms](xref:transforms.md) on the RequestProxyOptions.

### Error handling

IHttpProxy catches client exceptions and timeouts, logs them, and converts them to 5xx status codes or aborts the response. The error details, if any, can be accessed from the IProxyErrorFeature as shown above.
