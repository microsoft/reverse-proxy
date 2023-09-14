// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Transforms;

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

            services.AddSingleton<TokenService>();

            services.AddReverseProxy()
                .LoadFromConfig(_configuration.GetSection("ReverseProxy"))
                .AddTransforms(transformBuilderContext =>  // Add transforms inline
                {
                    // For each route+cluster pair decide if we want to add transforms, and if so, which?
                    // This logic is re-run each time a route is rebuilt.

                    // Only do this for routes that require auth.
                    if (string.Equals("myPolicy", transformBuilderContext.Route.AuthorizationPolicy))
                    {
                        transformBuilderContext.AddRequestTransform(async transformContext =>
                        {
                            // AuthN and AuthZ will have already been completed after request routing.
                            var ticket = await transformContext.HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            var tokenService = transformContext.HttpContext.RequestServices.GetRequiredService<TokenService>();
                            var token = await tokenService.GetAuthTokenAsync(ticket.Principal);

                            // Reject invalid requests
                            if (string.IsNullOrEmpty(token))
                            {
                                var response = transformContext.HttpContext.Response;
                                response.StatusCode = 401;
                                return;
                            }

                            transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        });
                    }
                }); ;

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
                endpoints.MapForwarder("{test}", "", c => c.AddPathRouteValues("{sss}"));
            });
        }
    }
}
