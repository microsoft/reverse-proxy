// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Yarp.ReverseProxy.Utilities;

internal sealed class ActivityCancellationTokenSource : CancellationTokenSource
{
    private const int MaxQueueSize = 1024;
    private static readonly ConcurrentQueue<ActivityCancellationTokenSource> _sharedSources = new();
    private static int _count;

    private static readonly Action<object?> _linkedTokenCancelDelegate = static s =>
    {
        var cts = (ActivityCancellationTokenSource)s!;
        cts.CancelledByLinkedToken = true;
        cts.Cancel(throwOnFirstException: false);
    };

    private int _activityTimeoutMs;
    private CancellationTokenRegistration _linkedRegistration1;
    private CancellationTokenRegistration _linkedRegistration2;

    private ActivityCancellationTokenSource() { }

    public bool CancelledByLinkedToken { get; private set; }

    public void ResetTimeout()
    {
        CancelAfter(_activityTimeoutMs);
    }

    public static ActivityCancellationTokenSource Rent(TimeSpan activityTimeout, CancellationToken linkedToken1 = default, CancellationToken linkedToken2 = default)
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
        cts._linkedRegistration1 = linkedToken1.UnsafeRegister(_linkedTokenCancelDelegate, cts);
        cts._linkedRegistration2 = linkedToken2.UnsafeRegister(_linkedTokenCancelDelegate, cts);
        cts.ResetTimeout();

        return cts;
    }

    public void Return()
    {
        _linkedRegistration1.Dispose();
        _linkedRegistration1 = default;
        _linkedRegistration2.Dispose();
        _linkedRegistration2 = default;

        if (TryReset())
        {
            Debug.Assert(!CancelledByLinkedToken);

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
