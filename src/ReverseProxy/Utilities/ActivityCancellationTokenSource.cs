// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Yarp.ReverseProxy.Utilities;

internal sealed class ActivityCancellationTokenSource : CancellationTokenSource
{
    private const int MaxQueueSize = 1024;
    private static readonly ConcurrentQueue<ActivityCancellationTokenSource> _sharedSources = new();
    private static int _count;

    private static readonly Action<object?> _linkedTokenCancelDelegate = static s =>
    {
        ((ActivityCancellationTokenSource)s!).Cancel(throwOnFirstException: false);
    };

    private int _activityTimeoutMs;
    private CancellationTokenRegistration _linkedRegistration;

    private ActivityCancellationTokenSource() { }

    public void ResetTimeout()
    {
        CancelAfter(_activityTimeoutMs);
    }

    public static ActivityCancellationTokenSource Rent(TimeSpan activityTimeout, CancellationToken linkedToken)
    {
        if (_sharedSources.TryDequeue(out var cts))
        {
            Interlocked.Decrement(ref _count);
        }
        else
        {
            cts = new ActivityCancellationTokenSource();
        }

        cts._activityTimeoutMs = (int)activityTimeout.TotalMilliseconds;
        cts._linkedRegistration = linkedToken.UnsafeRegister(_linkedTokenCancelDelegate, cts);
        cts.ResetTimeout();

        return cts;
    }

    public void Return()
    {
        _linkedRegistration.Dispose();
        _linkedRegistration = default;

        if (TryReset())
        {
            if (Interlocked.Increment(ref _count) <= MaxQueueSize)
            {
                _sharedSources.Enqueue(this);
                return;
            }

            Interlocked.Decrement(ref _count);
        }

        Dispose();
    }
}
