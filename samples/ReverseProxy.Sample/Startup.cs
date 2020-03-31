// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Core;
using Microsoft.ReverseProxy.Core.Configuration.DependencyInjection;
using Microsoft.ReverseProxy.Sample.Config;

namespace Microsoft.ReverseProxy.Sample
{
    /// <summary>
    /// ASP .NET Core pipeline initialization.
    /// </summary>
    public class Startup
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddReverseProxy();

            // The following 2 lines are all that we need to react to config changes on the fly.
            // You can then change appsettings.json on disk and we will apply the new configs without a restart.
            services.Configure<ProxyConfigRoot>(_configuration.GetSection("ReverseProxy"));
            services.AddHostedService<ProxyConfigApplier>();
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseReverseProxy();
        }
    }
}
