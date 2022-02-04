// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Configuration;
using Yarp.Kubernetes.Controller.Services;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.Kubernetes.Controller;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
#pragma warning disable CA1822 // Mark members as static
    public void ConfigureServices(IServiceCollection services)
#pragma warning restore CA1822 // Mark members as static
    {
        // Add components from the kubernetes controller framework
        services.AddKubernetesControllerRuntime();

        // Add components implemented by this application
        services.AddHostedService<IngressController>();
        services.AddSingleton<ICache, IngressCache>();
        services.Configure<YarpOptions>(_configuration.GetSection("Yarp"));

        // Register the necessary Kubernetes resource informers
        services.RegisterResourceInformer<V1Ingress>();
        services.RegisterResourceInformer<V1Service>();
        services.RegisterResourceInformer<V1Endpoints>();
        services.RegisterResourceInformer<V1IngressClass>();

        // Add the reverse proxy functionality
        services.AddReverseProxy();

        var provider = new InMemoryConfigProvider();
        services.AddSingleton<IProxyConfigProvider>(provider);
        services.AddSingleton<IUpdateConfig>(provider);
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
#pragma warning disable CA1822 // Mark members as static
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
#pragma warning restore CA1822 // Mark members as static
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapReverseProxy();
        });
    }
}
