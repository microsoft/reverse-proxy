// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Kubernetes.Resources;

namespace Microsoft.Kubernetes.Testing
{
    public class TestClusterStartup
    {
#pragma warning disable CA1822 // Mark members as static
        public void ConfigureServices(IServiceCollection services)
#pragma warning restore CA1822 // Mark members as static
        {
            // services.AddTransient<ResourceManager>();
            services.AddControllers().AddNewtonsoftJson();
            services.AddSingleton<ITestCluster, TestCluster>();
            services.AddTransient<IResourceSerializers, ResourceSerializers>();
        }

#pragma warning disable CA1822 // Mark members as static
        public void Configure(IApplicationBuilder app, ITestCluster cluster)
#pragma warning restore CA1822 // Mark members as static
        {
            if (app is null)
            {
                throw new System.ArgumentNullException(nameof(app));
            }

            if (cluster is null)
            {
                throw new System.ArgumentNullException(nameof(cluster));
            }

            app.Use(next => async context =>
            {
                // This is a no-op, but very convenient for setting a breakpoint to see per-request details.
                await next(context);
            });
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
            app.Run(cluster.UnhandledRequest);
        }
    }
}
