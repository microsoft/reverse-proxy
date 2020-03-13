// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace IslandGateway.Sample
{
    /// <summary>
    /// Class that contains the entrypoint for the Island Gateway sample app.
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
                    webBuilder.UseStartup<Startup>();
                });
    }
}
