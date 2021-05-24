# Minimal sample

This sample brings together the [Config-based YARP Sample] and [Minimal Hosting for ASP.NET Core] introduced in .NET 6.
As such, this sample project can only be used when targeting .NET 6+.

Turning an empty ASP.NET Core project (`dotnet new web`) into a functioning YARP application takes only 3 lines of code:
- Adding YARP services

  ```c#
  builder.Services.AddReverseProxy()
      .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
  ```
- Adding YARP to the request pipeline

  ```c#
  app.MapReverseProxy();
  ```
- Adding a `ReverseProxy` section to `appsettings.json`

  The [configuration file](appsettings.json) in this sample represents a minimal configuration for a basic YARP application.
  See the [Config-based YARP Sample configuration] for a more exhaustive list of settings exposed via configuration.

[Config-based YARP Sample]: ../ReverseProxy.Config.Sample
[Config-based YARP Sample configuration]: ../ReverseProxy.Config.Sample/appsettings.json
[Minimal Hosting for ASP.NET Core]: https://github.com/dotnet/aspnetcore/issues/30354
