# HTTP/3

## Introduction
To enable HTTP/3 protocol on YARP you should set up HttpClient and Kestrel.

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
For correct project build with `HttpProtocols.Http1AndHttp2AndHttp3` preview features need to be enabled:
```proj
<PropertyGroup>
  <EnablePreviewFeatures>True</EnablePreviewFeatures>
</PropertyGroup>
```

## HttpClient

There is the switch which enables HTTP/3 either programmatically or in project features:
```C#
// Set this switch programmatically or in csproj:
// <RuntimeHostConfigurationOption Include="System.Net.SocketsHttpHandler.Http3Support" Value="true" />
AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3Support", true);
```

In addition to this default version of HttpRequest should be replaced by "3", find more details about [HttpRequest configuration](http-client-config.md#httprequest).


