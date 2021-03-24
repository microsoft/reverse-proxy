// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Kubernetes.Controller.Hosting;
using Microsoft.Kubernetes.CustomResources;
using Microsoft.Kubernetes.Resources;
using Microsoft.Rest;
using Polly;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UsingCustomResources.Models;

namespace UsingCustomResources
{
    /// <summary>
    /// Program main entrypoint.
    /// </summary>
    /// <seealso cref="Microsoft.Kubernetes.Controller.Hosting.BackgroundHostedService" />
    public class Program : BackgroundHostedService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IConfiguration _configuration;
        private readonly ICustomResourceDefinitionGenerator _generator;
        private readonly IResourceSerializers _serializers;
        private readonly IKubernetes _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        /// <param name="hostApplicationLifetime">The host application lifetime.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="client">The client.</param>
        /// <param name="generator">The generator.</param>
        /// <param name="serializers">The serializers.</param>
        public Program(
            IHostApplicationLifetime hostApplicationLifetime,
            IConfiguration configuration,
            ILogger<Program> logger,
            IKubernetes client,
            ICustomResourceDefinitionGenerator generator,
            IResourceSerializers serializers)
            : base(hostApplicationLifetime, logger)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
            _configuration = configuration;
            _client = client;
            _generator = generator;
            _serializers = serializers;
        }

        /// <summary>
        /// Main entrypoint for console application.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>Process exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var hostBuilder = new HostBuilder();

                hostBuilder.ConfigureHostConfiguration(hostConfiguration =>
                {
                    hostConfiguration.AddCommandLine(args);
                });

                hostBuilder.ConfigureServices(services =>
                {
                    services.AddLogging(logging => logging.AddConsole());
                    services.AddKubernetesCore();
                    services.AddKubernetesCustomResources();
                    services.AddHostedService<Program>();
                });

                await hostBuilder.RunConsoleAsync();
                return 0;
            }
            catch (HttpOperationException error)
            {
                Console.WriteLine(error);
                Console.WriteLine(error.Response.Content);
            }
            catch (AggregateException error)
            {
                error.Handle(ex =>
                {
                    Console.WriteLine(ex);
                    if (ex is HttpOperationException opex)
                    {
                        Console.WriteLine(opex.Response.Content);
                    }
                    return true;
                });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception error)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Console.WriteLine(error);
            }
            return 1;
        }

        /// <summary>
        /// Runs the asynchronous background work.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            // One way to establish CRD is to write them out, possibly from a utility project
            // or build step, and deploy to the later through normal mechanisms

            var outputCrds = _configuration["output-crds"];
            if (!string.IsNullOrEmpty(outputCrds))
            {
                await WriteToCustomResourceDefinitionsYamlFileAsync(outputCrds, cancellationToken);
            }

            // Another way is to create or update CRD on startup. This can be a simpler way
            // to get started quickly, but has the disadvantage that the program needs to run
            // with a ClusterRoleBinding that allows CRD editing.

            await ApplyTicketProviderDefinitionAsync(cancellationToken);

            await ApplyTicketDefinitionAsync(cancellationToken);

            // The rest of these examples manipulate custom resources using either the
            // API Server endpoint or the kubectl CLI. In either case this will be
            // connecting with your default cluster, or if running in a Pod, will
            // talk to the cluster itself using the Pod's ServiceAccount bearer token.

            await ApplyTicketProviderAsync(cancellationToken);

            var ticket = await CreateTicketAsync(cancellationToken);

            ticket = await PatchTicketStatusAsync(ticket, cancellationToken);

            // use cli to show ticketing schema and documentation text from the CRDs
            ShellExecute("kubectl", "explain", "ticketproviders");
            ShellExecute("kubectl", "explain", "ticketproviders.spec", "--recursive");
            ShellExecute("kubectl", "explain", "tickets");
            ShellExecute("kubectl", "explain", "tickets.spec");

            // use cli to list ticketing resources showing column defined by CRDs
            ShellExecute("kubectl", "get", "ticketproviders");
            ShellExecute("kubectl", "get", "ticketproviders", "--output", "wide");
            ShellExecute("kubectl", "get", "tickets", "--all-namespaces");
            ShellExecute("kubectl", "get", "tickets", "--all-namespaces", "--output", "wide");

            ShellExecute("kubectl", "describe", "ticket", "--namespace", ticket.Namespace(), ticket.Name());

            _hostApplicationLifetime.StopApplication();
        }

        private async Task ApplyTicketProviderDefinitionAsync(CancellationToken cancellationToken)
        {
            // build and apply TicketProvider CRD
            var ticketProviderCrd = await _generator.GenerateCustomResourceDefinitionAsync<V1alpha1TicketProvider>("Cluster", cancellationToken);
            ticketProviderCrd.Spec.Versions.Single().AdditionalPrinterColumns = new[]
            {
                new V1CustomResourceColumnDefinition(name:"Type", type:"string", jsonPath:".spec.type"),
                new V1CustomResourceColumnDefinition(name:"Online", type:"boolean", jsonPath:".status.online"),
            };

            var tickerProviderCrdResult = await _client.CreateOrReplaceCustomResourceDefinitionAsync(ticketProviderCrd, cancellationToken);
            Logger.LogInformation("Updated {Kind} {Name} {ResourceVersion}", tickerProviderCrdResult.Kind, tickerProviderCrdResult.Name(), tickerProviderCrdResult.ResourceVersion());
        }

        private async Task ApplyTicketDefinitionAsync(CancellationToken cancellationToken)
        {
            // build and apply Ticket CRD
            var ticketCrd = await _generator.GenerateCustomResourceDefinitionAsync<V1alpha1Ticket>("Namespaced", cancellationToken);
            ticketCrd.Spec.Versions.Single().AdditionalPrinterColumns = new[]
            {
                new V1CustomResourceColumnDefinition(name:"Severity", type:"string", jsonPath:".spec.severity"),
                new V1CustomResourceColumnDefinition(name:"Title", type:"string", jsonPath:".spec.title"),
                new V1CustomResourceColumnDefinition(name:"Provider", type:"string", jsonPath:".spec.providerClass", priority: 1),
                new V1CustomResourceColumnDefinition(name:"Id", type:"string", jsonPath:".status.uniqueId", priority: 1),
                new V1CustomResourceColumnDefinition(name:"State", type:"string", jsonPath:".status.workflowState"),
                new V1CustomResourceColumnDefinition(name:"Contact", type:"string", jsonPath:".status.contactInfo"),
            };

            var ticketCrdResult = await _client.CreateOrReplaceCustomResourceDefinitionAsync(ticketCrd, cancellationToken);
            Logger.LogInformation("Updated {Kind} {Name} {ResourceVersion}", ticketCrdResult.Kind, ticketCrdResult.Name(), ticketCrdResult.ResourceVersion());
        }

        private async Task WriteToCustomResourceDefinitionsYamlFileAsync(string fileName, CancellationToken cancellationToken)
        {
            var ticketCrd = await _generator.GenerateCustomResourceDefinitionAsync<V1alpha1Ticket>("Namespaced", cancellationToken);
            var ticketProviderCrd = await _generator.GenerateCustomResourceDefinitionAsync<V1alpha1TicketProvider>("Cluster", cancellationToken);

            using var file = File.Create(fileName);
            using var writer = new StreamWriter(file);
            await writer.WriteLineAsync("#------------------------------------------------------------------------------");
            await writer.WriteLineAsync("# <auto-generated>");
            await writer.WriteLineAsync("#     This code was generated by a tool.");
            await writer.WriteLineAsync("#");
            await writer.WriteLineAsync("#     Changes to this file may cause incorrect behavior and will be lost if");
            await writer.WriteLineAsync("#     the code is regenerated.");
            await writer.WriteLineAsync("# </auto-generated>");
            await writer.WriteLineAsync("#------------------------------------------------------------------------------");
            await writer.WriteLineAsync(Yaml.SaveToString(ticketProviderCrd));
            await writer.WriteLineAsync("---");
            await writer.WriteLineAsync(Yaml.SaveToString(ticketCrd));
        }

        private async Task ApplyTicketProviderAsync(CancellationToken cancellationToken)
        {
            // apply an example TicketProvider instance
            var ticketProvider = new V1alpha1TicketProvider
            {
                ApiVersion = $"{V1alpha1TicketProvider.KubeGroup}/{V1alpha1TicketProvider.KubeApiVersion}",
                Kind = V1alpha1TicketProvider.KubeKind,
                Metadata = new V1ObjectMeta(
                    name: "github-operatorframework-tickets"),
                Spec = new V1alpha1TicketProviderSpec
                {
                    Type = "gitHub",
                    GitHub = new V1alpha1TicketProviderSpecGitHub
                    {
                        Organization = "Microsoft",
                        Repository = "OperatorFramework",
                        SecretName = "github-operatorframework-tickets",
                    }
                }
            };

            var tickerProviderResult = await _client.CreateOrReplaceClusterCustomObjectAsync(
                V1alpha1TicketProvider.KubeGroup,
                V1alpha1TicketProvider.KubeApiVersion,
                "ticketproviders",
                ticketProvider,
                cancellationToken);

            Logger.LogInformation("Updated {Kind} {Name} {ResourceVersion}", tickerProviderResult.Kind, tickerProviderResult.Name(), tickerProviderResult.ResourceVersion());
        }

        private async Task<V1alpha1Ticket> CreateTicketAsync(CancellationToken cancellationToken)
        {
            // create a new ticket each time the example runs
            var ticket = new V1alpha1Ticket
            {
                ApiVersion = $"{V1alpha1Ticket.KubeGroup}/{V1alpha1Ticket.KubeApiVersion}",
                Kind = V1alpha1Ticket.KubeKind,
                Metadata = new V1ObjectMeta(
                    name: $"example-{new Random().Next(1000, 10000)}",
                    namespaceProperty: "default"),
                Spec = new V1alpha1TicketSpec
                {
                    ProviderClass = "github-operatorframework-tickets",
                    Severity = "low",
                    Title = $"Example ticket created for process {Process.GetCurrentProcess().Id}"
                }
            };

            var ticketResult = await _client.CreateOrReplaceNamespacedCustomObjectAsync(
                V1alpha1Ticket.KubeGroup,
                V1alpha1Ticket.KubeApiVersion,
                "tickets",
                ticket,
                cancellationToken);

            Logger.LogInformation("Updated {Kind} {Name}.{Namespace} {ResourceVersion}", ticketResult.Kind, ticketResult.Name(), ticketResult.Namespace(), ticketResult.ResourceVersion());

            return ticketResult;
        }

        private async Task<V1alpha1Ticket> PatchTicketStatusAsync(V1alpha1Ticket ticket, CancellationToken cancellationToken)
        {
            var policy = Policy
                .Handle<HttpOperationException>(ex => ex.Response.StatusCode == HttpStatusCode.NotFound)
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(25 * Math.Pow(2, attempt)));

            var status = new V1alpha1TicketStatus
            {
                UniqueId = Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture),
                WorkflowState = "Created",
                ContactInfo = "ticketing@example.io",
            };

            return await policy.ExecuteAsync(async () =>
            {
                var result = await _client.PatchNamespacedCustomObjectAsync(
                    new V1alpha1Ticket { Status = status },
                    group: V1alpha1Ticket.KubeGroup,
                    version: V1alpha1Ticket.KubeApiVersion,
                    namespaceParameter: ticket.Namespace(),
                    plural: "tickets",
                    name: ticket.Name(),
                    cancellationToken: cancellationToken);

                return _serializers.Convert<V1alpha1Ticket>(result);
            });
        }

        private int ShellExecute(string command, params string[] arguments)
        {
            Logger.LogInformation("Executing {Command} {Arguments}", command, string.Join(' ', arguments));

            var startInfo = new ProcessStartInfo(command);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            var process = Process.Start(startInfo);
            process.WaitForExit();
            return process.ExitCode;
        }
    }
}
