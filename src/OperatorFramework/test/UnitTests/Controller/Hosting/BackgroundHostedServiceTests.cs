// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Kubernetes.Controller.Hosting.Fakes;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Kubernetes.Controller.Hosting
{
    public class BackgroundHostedServiceTests
    {
        [Fact]
        public async Task StartAndStopUnderHosting()
        {
            var latches = new TestLatches();

            using var host = new HostBuilder()
                .ConfigureServices((hbc, services) =>
                {
                    services.AddSingleton<IHostedService, FakeBackgroundHostedService>();
                    services.AddSingleton(latches);
                })
                .Build();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await host.StartAsync(cts.Token).ConfigureAwait(false);
            await latches.RunEnter.WhenSignalAsync(cts.Token).ConfigureAwait(false);
            latches.RunResult.Signal();
            await latches.RunExit.WhenSignalAsync(cts.Token).ConfigureAwait(false);
            await host.StopAsync(cts.Token).ConfigureAwait(false);
        }

        [Fact]
        public async Task StartAndStopUnderWebHost()
        {
            var latches = new TestLatches();

            using var host = new WebHostBuilder()
                .ConfigureServices((hbc, services) =>
                {
                    services.AddSingleton<IServer, FakeServer>();
                    services.AddSingleton<IHostedService, FakeBackgroundHostedService>();
                    services.AddSingleton(latches);
                })
                .Configure(app => { })
                .Build();

            // TODO: figure out why the hosting takes so long to unwind naturally
            // and increase this safety cancellation up from 3 seconds
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            var runTask = host.RunAsync(cts.Token);

            await latches.RunEnter.WhenSignalAsync(cts.Token).ConfigureAwait(false);
            latches.RunResult.Signal();
            await latches.RunExit.WhenSignalAsync(cts.Token).ConfigureAwait(false);

            await runTask.ConfigureAwait(false);
        }


        [Fact]
        public async Task IfRunAsyncThrowsItComesBackFromHost()
        {
            var context = new TestLatches();

            using var host = new WebHostBuilder()
                .ConfigureServices((hbc, services) =>
                {
                    services.AddSingleton<IServer, FakeServer>();
                    services.AddSingleton<IHostedService, FakeBackgroundHostedService>();
                    services.AddSingleton(context);
                })
                .Configure(app => { })
                .Build();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var runTask = host.RunAsync(cts.Token);

#pragma warning disable CA1303 // Do not pass literals as localized parameters
            context.RunResult.Throw(new ApplicationException("Unwind"));
#pragma warning restore CA1303 // Do not pass literals as localized parameters

            var ex = await Assert.ThrowsAsync<AggregateException>(() => runTask);

            Assert.Equal("Unwind", Assert.Single(ex.Flatten().InnerExceptions).Message);
        }
    }
}
