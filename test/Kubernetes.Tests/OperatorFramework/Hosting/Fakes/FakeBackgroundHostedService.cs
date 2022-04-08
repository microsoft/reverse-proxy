// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Hosting;
using Microsoft.Kubernetes.Controller.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.OperatorFramework.Hosting.Fakes;

public class FakeBackgroundHostedService : BackgroundHostedService
{
    private readonly TestLatches _context;

    public FakeBackgroundHostedService(
        TestLatches context,
        IHostApplicationLifetime hostApplicationLifetime)
        : base(hostApplicationLifetime, null)
    {
        _context = context;
    }

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            _context.RunEnter.Signal();
            await _context.RunResult.WhenSignalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _context.RunExit.Signal();
        }
    }
}
