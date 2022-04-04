# HTTP/3

## Introduction
YARP supports HTTP/3 for inbound and outbound connections using the HTTP/3 preview support in .NET 6. To enable the HTTP/3 protocol in YARP you need to:
- Configure inbound connections in Kestrel
- Configure outbound connections in HttpClient 
- Enable preview features

## Set up HTTP/3 on Kestrel

Protocols are required in the listener options:
```C#
var myHostBuilder = Host.CreateDefaultBuilder(args);
myHostBuilder.ConfigureWebHostDefaults(webHostBuilder =>
    {
        webHostBuilder.ConfigureKestrel(kestrel =>
        {
            kestrel.ListenAnyIP(443, portOptions =>
            {
#if NET6_0_OR_GREATER
                portOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
#endif
            portOptions.UseHttps();
        });
    });
    webHostBuilder.UseStartup<Startup>();
});
```
To use HTTP/3 with .NET 6, preview features need to be enabled via a setting in the project file:
```proj
<PropertyGroup>
  <EnablePreviewFeatures>True</EnablePreviewFeatures>
</PropertyGroup>
```

## HttpClient

There is the switch which enables HTTP/3 either programmatically:
```C#
AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3Support", true);
```
or in project feature:
```csproj
<RuntimeHostConfigurationOption Include="System.Net.SocketsHttpHandler.Http3Support" Value="true" />
```

In addition to this default version of HttpRequest should be replaced by "3", find more details about [HttpRequest configuration](http-client-config.md#httprequest).


