// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace Yarp.Kubernetes.Ingress
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            using var serilog = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .Enrich.FromLogContext()
               .WriteTo.Console(theme: AnsiConsoleTheme.Code)
               .CreateLogger();

            ServiceClientTracing.IsEnabled = true;

            Host.CreateDefaultBuilder(args)
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseKubernetesReverseProxyCertificateSelector();
                })
                .ConfigureAppConfiguration(config =>
                {
                    config.AddJsonFile("/app/config/yarp.json", optional: true);
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSerilog(serilog, dispose: false);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                }).Build().Run();
        }
    }
}
