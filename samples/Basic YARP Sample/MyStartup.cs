using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BasicYARPSample
{
    // Provides a configuration for the kestrel server with reverse proxy middleware
    public class MyStartup
    {
        public MyStartup(IConfiguration configuration)
        {
            // Default configuration comes from AppSettings.json file in project/output
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add capabilities to
        // the web server via services in the DI container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add the reverse proxy to capability to the server
            var ProxyBuilder = services.AddReverseProxy();
            // Initialize the reverse proxy from the "ReverseProxy" section of configuration
            ProxyBuilder.LoadFromConfig(Configuration.GetSection("ReverseProxy"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline for each request
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            // Enable endpoint routing, required for the reverse proxy
            app.UseRouting();
            
            // TODO: should this be removed: app.UseAuthorization();

            // Configure EndPoints to use the reverse proxy
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();
            });
        }
    }
}
