// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Yarp.ReverseProxy.Utilities
{
    internal sealed class ActivityCancellationTokenSource : CancellationTokenSource
    {
#if NET6_0_OR_GREATER
        private static readonly ConcurrentQueue<ActivityCancellationTokenSource> _sharedSources = new();
#endif

        public static readonly Action<object?> LinkedTokenCancelDelegate = static s =>
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
#if NET6_0_OR_GREATER
            if (!_sharedSources.TryDequeue(out var cts))
            {
                cts = new ActivityCancellationTokenSource();
            }
#else
            var cts = new ActivityCancellationTokenSource();
#endif

            cts._activityTimeoutMs = (int)activityTimeout.TotalMilliseconds;
            cts._linkedRegistration = linkedToken.UnsafeRegister(LinkedTokenCancelDelegate, cts);
            cts.ResetTimeout();

            return cts;
        }

        public void Return()
        {
            _linkedRegistration.Dispose();
            _linkedRegistration = default;

#if NET6_0_OR_GREATER
            if (TryReset())
            {
                _sharedSources.Enqueue(this);
                return;
            }
#endif

            Dispose();
        }
    }
}
