// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace BenchmarkApp
{
    public class Program
    {
        public static void Main(string[] args) =>
            CreateHostBuilder(args).Build().Run();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                    webBuilder.ConfigureKestrel((context, kestrelOptions) =>
                    {
                        kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
                        {
                            httpsOptions.ServerCertificate = new X509Certificate2(Path.Combine(context.HostingEnvironment.ContentRootPath, "testCert.pfx"), "testPassword");
                        });
                    })
                   .UseStartup<Startup>());
    }
}
