// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Dispatching;
using Yarp.Kubernetes.Controller.Services;

namespace Yarp.Kubernetes.Controller;

public class Startup
{
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
        services.AddTransient<IReconciler, Reconciler>();
        services.AddSingleton<IDispatcher, Dispatcher>();

        // Register the necessary Kubernetes resource informers
        services.RegisterResourceInformer<V1Ingress>();
        services.RegisterResourceInformer<V1Service>();
        services.RegisterResourceInformer<V1Endpoints>();

        // Add ASP.NET Core controller support
        services.AddControllers();
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
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
        });
    }
}
