// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace PodStatusConditions
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            await new HostBuilder()
                .ConfigureHostConfiguration(builder => builder.AddCommandLine(args))
                .ConfigureServices(ConfigureServices)
                .RunConsoleAsync()
                .ConfigureAwait(false);
        }

        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(logging => logging.AddConsole());

            services.AddKubernetesControllerRuntime();

            services.AddHostedService<StatusController>();

            services.RegisterResourceInformer<V1Pod>();
            services.RegisterResourceInformer<V1ConfigMap>();
        }
    }
}
