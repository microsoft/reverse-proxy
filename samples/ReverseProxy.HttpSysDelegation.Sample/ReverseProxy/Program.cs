using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

Debug.Assert(OperatingSystem.IsWindows());
builder.WebHost.UseHttpSys();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddHttpSysDelegation();

var app = builder.Build();

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseSessionAffinity(); // Has no affect on delegation destinations because the response doesn't go through YARP
    proxyPipeline.UseLoadBalancing();
    proxyPipeline.UsePassiveHealthChecks();
    proxyPipeline.UseHttpSysDelegation();
});

app.Run();
