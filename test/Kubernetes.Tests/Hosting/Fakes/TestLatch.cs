// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Tests.Hosting.Fakes;

public class TestLatch
{
    private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>();

    public void Signal()
    {
        _completion.SetResult(false);
    }
    public void Throw(ApplicationException exception)
    {
        _completion.SetException(exception);
    }

    public async Task WhenSignalAsync(CancellationToken cancellationToken)
    {
        var task = await Task.WhenAny(
            _completion.Task,
            Task.Delay(TimeSpan.FromSeconds(90), cancellationToken))
            .ConfigureAwait(false);

        await task.ConfigureAwait(false);
    }
}
