// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yarp.ReverseProxy.Kubernetes.Controller.Caching;
using Yarp.ReverseProxy.Kubernetes.Controller.Dispatching;
using Yarp.ReverseProxy.Kubernetes.Controller.Services;

namespace Yarp.ReverseProxy.Kubernetes.Controller
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Add components from the kubernetes controller framweork
            services.AddKubernetesControllerRuntime();

            // Add components implemented by this application
            services.AddHostedService<Services.Controller>();
            services.AddSingleton<ICache, Cache>();
            services.AddTransient<IReconciler, Reconciler>();
            services.AddSingleton<IDispatcher, Dispatcher>();

            // Register the necessary Kubernetes resource infromers
            services.RegisterResourceInformer<V1Ingress>();
            services.RegisterResourceInformer<V1Endpoints>();

            // Add ASP.NET Core controller support
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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
}
