// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Backend
{
    public class Startup
    {
        private readonly JsonSerializerOptions options = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    var backendInfo = new BackendInfo()
                    {
                        IP = context.Connection.LocalIpAddress.ToString(),
                        Hostname = Dns.GetHostName(),
                    };

                    context.Response.ContentType = "application/json; charset=utf-8";
                    await JsonSerializer.SerializeAsync(context.Response.Body, backendInfo);
                });
            });
        }

        private class BackendInfo
        {
            public string IP { get; set; } = default!;

            public string Hostname { get; set; } = default!;
        }
    }
}
