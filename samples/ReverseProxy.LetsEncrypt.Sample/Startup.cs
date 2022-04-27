using LettuceEncrypt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Yarp.Sample
{
    // Sets up the ASP.NET application with the reverse proxy enabled.
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            // Default configuration comes from AppSettings.json file in project/output
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add capabilities to
        // the web application via services in the DI container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLettuceEncrypt();

            // Add the reverse proxy capability to the server
            var proxyBuilder = services.AddReverseProxy();
            // Initialize the reverse proxy from the "ReverseProxy" section of configuration
            proxyBuilder.LoadFromConfig(Configuration.GetSection("ReverseProxy"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request 
        // pipeline that handles requests
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            // Enable endpoint routing, required for the reverse proxy
            app.UseRouting();
            // Register the reverse proxy routes
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();
            });
        }
    }
}
