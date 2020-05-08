// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace BenchmarkApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
#if !DEBUG
                    // The benchmark infrastructure seems to default to a development environment, but it always uses Release builds.
                    webBuilder.UseEnvironment(Environments.Production);
#endif
                    webBuilder.UseStartup<Startup>();
                });
    }
}
