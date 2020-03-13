// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SampleServer
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
            services.AddControllers()
                .AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            app.UseHttpsRedirection();

            app.UseWebSockets();

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
