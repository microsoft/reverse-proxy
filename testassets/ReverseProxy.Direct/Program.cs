// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.ReverseProxy.Sample
{
    /// <summary>
    /// Class that contains the entrypoint for the Reverse Proxy sample app.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entrypoint of the application.
        /// </summary>
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(kestrel =>
                    {
                        var logger = kestrel.ApplicationServices.GetRequiredService<ILogger<Program>>();
                        kestrel.ListenAnyIP(5001, portOptions =>
                        {
                            portOptions.Use(async (connectionContext, next) =>
                            {
                                await TlsFilter.ProcessAsync(connectionContext, next, logger);
                            });
                            portOptions.UseHttps();
                        });
                    });
                    webBuilder.UseStartup<Startup>();
                });
    }
}
