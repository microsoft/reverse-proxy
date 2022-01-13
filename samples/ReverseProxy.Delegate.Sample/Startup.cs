using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;
using Yarp.Sample;

namespace Yarp.Sample
{
    public class Startup
    {
       
        public Startup()
        {
            
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
           
#if !NET6_0_OR_GREATER
            // Workaround the lack of distributed tracing support in SocketsHttpHandler before .NET 6.0
            services.AddSingleton<IForwarderHttpClientFactory, DiagnosticsHandlerFactory>();
#endif
            services.AddSingleton<IReproConfig,ReproConfig>();

            services.AddControllers();
            var serviceProvider = services.BuildServiceProvider();

            var _reproConfig = serviceProvider.GetService<IReproConfig>();
            // Specify a custom proxy config provider, in this case defined in InMemoryConfigProvider.cs
            // Programatically creating route and cluster configs. This allows loading the data from an arbitrary source.
            
            services.AddReverseProxy()
                .LoadFromMemory(_reproConfig.GetRoutes(), _reproConfig.GetClusters());
            
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                // We can customize the proxy pipeline and add/remove/replace steps
                endpoints.MapReverseProxy(proxyPipeline =>
                {
                    // Use a custom proxy middleware, defined below
                    //proxyPipeline.Use(MyCustomProxyStep);
                    // Don't forget to include these two middleware when you make a custom proxy pipeline (if you need them).
                    //proxyPipeline.UseSessionAffinity();
                    proxyPipeline.UseLoadBalancing();
                });
                
                //.MapGet("/", async context => { await context.Response.WriteAsync("Hello!"); });
            });
        }
    }
}
