// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Controller.Hosting.Fakes
{
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
}
