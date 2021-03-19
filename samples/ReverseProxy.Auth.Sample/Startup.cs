// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Yarp.Sample
{
    /// <summary>
    /// ASP .NET Core pipeline initialization.
    /// </summary>
    public class Startup
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup" /> class.
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
            // Required to supply the authentication UI in Views/*
            services.AddRazorPages();

            services.AddReverseProxy()
                .LoadFromConfig(_configuration.GetSection("ReverseProxy"));

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();

            services.AddAuthorization(options =>
            {
                // Creates a policy called "myPolicy" that depends on having a claim "myCustomClaim" with the value "green".
                // See AccountController.Login method for where this claim is applied to the user identity
                // This policy can then be used by routes in the proxy, see "ClaimsAuthRoute" in appsettings.json
                options.AddPolicy("myPolicy", builder => builder
                    .RequireClaim("myCustomClaim", "green")
                    .RequireAuthenticatedUser());

                // The default policy is to require authentication, but no additional claims
                // Uncommenting the following would have no effect
                // options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

                // FallbackPolicy is used for routes that do not specify a policy in config
                // Make all routes that do not specify a policy to be anonymous (this is the default).
                options.FallbackPolicy = null; 
                // Or make all routes that do not specify a policy require some auth:
                // options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();            
            });
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            // The order of these is important as it defines the steps that will be used to handle each request
            app.UseDeveloperExceptionPage();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapReverseProxy();
            });
        }
    }
}
