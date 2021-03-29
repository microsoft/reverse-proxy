// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BasicOperator.Generators;
using BasicOperator.Models;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Kubernetes.ResourceKinds;
using Microsoft.Kubernetes.ResourceKinds.OpenApi;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Threading.Tasks;

namespace BasicOperator
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            using var serilog = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                .CreateLogger();

            var hostBuilder = new HostBuilder();

            hostBuilder.ConfigureHostConfiguration(hostConfiguration =>
            {
                hostConfiguration.AddCommandLine(args);
            });

            hostBuilder.ConfigureServices(services =>
            {
                services.AddLogging(logging =>
                {
                    logging.AddSerilog(serilog, dispose: false);
                });

                services.AddTransient<IResourceKindProvider, OpenApiResourceKindProvider>();

                services.AddCustomResourceDefinitionUpdater<V1alpha1HelloWorld>(options =>
                {
                    options.Scope = "Namespaced";
                });

                services.AddOperator<V1alpha1HelloWorld>(settings =>
                {
                    settings
                        .WithRelatedResource<V1Deployment>()
                        .WithRelatedResource<V1ServiceAccount>()
                        .WithRelatedResource<V1Service>();

                    settings.WithGenerator<HelloWorldGenerator>();
                });
            });

            await hostBuilder.RunConsoleAsync();
            return 0;
        }
    }
}
