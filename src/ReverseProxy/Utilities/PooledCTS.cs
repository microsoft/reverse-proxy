// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Yarp.ReverseProxy.Utilities
{
    internal sealed class PooledCTS : CancellationTokenSource
    {
        private static readonly ConcurrentQueue<PooledCTS> _sharedSources = new();

        private static readonly Action<object?> _linkedTokenCancelDelegate = static s =>
        {
            ((CancellationTokenSource)s!).Cancel(throwOnFirstException: false);
        };

        private const long SafeToReuseTicks = TimeSpan.TicksPerSecond * 10;
        private static readonly double _stopwatchTicksPerTimeSpanTick = Stopwatch.Frequency / (double)TimeSpan.TicksPerSecond;

        private long _safeToReuseBeforeTimestamp;
        private CancellationTokenRegistration _registration;

        public static PooledCTS Rent(TimeSpan timeout, CancellationToken linkedToken)
        {
            if (!_sharedSources.TryDequeue(out var cts))
            {
                cts = new PooledCTS();
            }

            cts._registration = linkedToken.UnsafeRegister(_linkedTokenCancelDelegate, cts);

            cts._safeToReuseBeforeTimestamp = Stopwatch.GetTimestamp() + (long)((timeout.Ticks - SafeToReuseTicks) * _stopwatchTicksPerTimeSpanTick);

            cts.CancelAfter(timeout);

            return cts;
        }

        public void Return()
        {
            _registration.Dispose();
            _registration = default;

            // TODO: Use TryReset in 6.0+
            CancelAfter(Timeout.Infinite);

            if (IsCancellationRequested || Stopwatch.GetTimestamp() > _safeToReuseBeforeTimestamp)
            {
                Dispose();
            }
            else
            {
                _sharedSources.Enqueue(this);
            }
        }
    }
}
