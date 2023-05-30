// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Hashing;
using System;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
            services.AddControllers();
            services.AddReverseProxy()
                .LoadFromConfig(_configuration.GetSection("ReverseProxy"))
                .AddTransforms<MyTransformProvider>() // Adds custom transforms via code.
                .AddTransformFactory<MyTransformFactory>() // Adds custom transforms via config.                                        
                .AddTransforms(transformBuilderContext =>  // Add transforms inline
                {
                    // For each route+cluster pair decide if we want to add transforms, and if so, which?
                    // This logic is re-run each time a route is rebuilt.

                    if (transformBuilderContext.Cluster.SessionAffinity?.Enabled == true)
                    {
                        var cookieName = transformBuilderContext.Cluster.SessionAffinity?.AffinityKeyName;
                        transformBuilderContext.AddRequestTransform(transformContext =>
                        {
                            // Does it already exist?
                            var cookies = transformContext.HttpContext.Request.Headers.Cookie.ToString();
                            if (cookies.Contains(cookieName + "="))
                            {
                                return default;
                            }

                            // Where are we proxying to?
                            var proxyFeature = transformContext.HttpContext.GetReverseProxyFeature();
                            var destinationId = proxyFeature.ProxiedDestination.DestinationId;

                            // See HashCookieSessionAffinityPolicy
                            var destinationIdBytes = Encoding.Unicode.GetBytes(destinationId.ToUpperInvariant());
                            var hashBytes = XxHash64.Hash(destinationIdBytes);
                            var affinity = Convert.ToHexString(hashBytes).ToLowerInvariant();

                            // Append to any existing cookies.
                            transformContext.HttpContext.Request.Headers.Cookie = $"{cookieName}={affinity}; {cookies}";

                            return default;
                        });
                    }

                    transformBuilderContext.AddPathPrefix("/prefix");

                    // Only do this for routes that require auth.
                    if (string.Equals("token", transformBuilderContext.Route.AuthorizationPolicy))
                    {
                        transformBuilderContext.AddRequestTransform(async transformContext =>
                        {
                            // AuthN and AuthZ will have already been completed after request routing.
                            var ticket = await transformContext.HttpContext.AuthenticateAsync("token");
                            var tokenService = transformContext.HttpContext.RequestServices.GetRequiredService<TokenService>();
                            var token = await tokenService.GetAuthTokenAsync(ticket.Principal);
                            transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        });
                    }
                });
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();
            });
        }
    }
}
