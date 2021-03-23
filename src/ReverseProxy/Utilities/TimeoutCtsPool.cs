// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Yarp.ReverseProxy.Utilities
{
    internal sealed class TimeoutCtsPool : IDisposable
    {
        // How accurate we want very short timeouts to be.
        private const int MinResolutionRatio = 10;

        private static readonly ConcurrentQueue<PooledCts> _sharedSources = new();

        private static void ReturnToSharedPool(PooledCts cts)
        {
            cts._pool = null;
            cts.CancelAfter(Timeout.Infinite);

            if (!cts.IsCancellationRequested)
            {
                _sharedSources.Enqueue(cts);
            }
        }

        private static PooledCts RentFromSharedPool(TimeSpan timeout)
        {
            if (!_sharedSources.TryDequeue(out var cts))
            {
                cts = new PooledCts();
            }
            cts.CancelAfter(timeout);
            return cts;
        }

        private readonly TimeSpan _maxTimeout;
        private readonly TimeSpan _minTimeout;
        private readonly double _inverseResolution;
        private readonly Timer _timer;
        private readonly PoolSegment[] _pools;
        private readonly PoolSegment _defaultPool;

        internal sealed class PoolSegment
        {
            private ConcurrentQueue<PooledCts> _newQueue = new();
            private ConcurrentQueue<PooledCts> _oldQueue = new();
            private ConcurrentQueue<PooledCts> _previousOldQueue = new();

            private long _version = 0;
            private bool _oldQueueIsEmptyHint = true;

            public PooledCts Rent(TimeSpan timeout)
            {
                PooledCts? cts;

                if (!_oldQueueIsEmptyHint)
                {
                    if (_oldQueue.TryDequeue(out cts))
                    {
                        return cts;
                    }
                    else
                    {
                        _oldQueueIsEmptyHint = true;
                    }
                }

                if (!_newQueue.TryDequeue(out cts))
                {
                    cts = RentFromSharedPool(timeout);
                    cts._pool = this;
                    cts._version = _version;
                }

                return cts;
            }

            public void Return(PooledCts cts)
            {
                var versionDelta = _version - cts._version;

                if (versionDelta == 0)
                {
                    _newQueue.Enqueue(cts);
                }
                else if (versionDelta == 1)
                {
                    _oldQueue.Enqueue(cts);

                    if (_oldQueueIsEmptyHint && _oldQueue.Count >= Environment.ProcessorCount)
                    {
                        _oldQueueIsEmptyHint = false;
                    }
                }
                else
                {
                    ReturnToSharedPool(cts);
                }
            }

            public void Reset()
            {
                while (_previousOldQueue.TryDequeue(out var cts))
                {
                    ReturnToSharedPool(cts);
                }

                var previousNewQueue = _newQueue;
                _newQueue = _previousOldQueue;
                _previousOldQueue = _oldQueue;
                _oldQueue = previousNewQueue;
                _version++;

                _oldQueueIsEmptyHint = false;
            }

            public void Dispose()
            {
                _version += long.MaxValue / 2;

                foreach (var queue in new[] { _newQueue, _oldQueue, _previousOldQueue })
                {
                    while (queue.TryDequeue(out var cts))
                    {
                        ReturnToSharedPool(cts);
                    }
                }
            }
        }

        public sealed class PooledCts : CancellationTokenSource
        {
            private static readonly Action<object?> _linkedTokenCancelDelegate = static s =>
            {
                ((CancellationTokenSource)s!).Cancel(throwOnFirstException: false);
            };

            internal PoolSegment? _pool;
            internal long _version;

            private CancellationTokenRegistration _registration;

            public void Register(CancellationToken linkedToken)
            {
                Debug.Assert(_registration == default);
                _registration = linkedToken.UnsafeRegister(_linkedTokenCancelDelegate, this);
            }

            public void Return()
            {
                _registration.Dispose();
                _registration = default;

                var pool = _pool;
                if (pool is null)
                {
                    ReturnToSharedPool(this);
                }
                else
                {
                    pool.Return(this);
                }
            }
        }

        public TimeoutCtsPool(TimeSpan maxTimeout, TimeSpan resolution)
        {
            if (maxTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTimeout), "Max timeout has to be a positive value.");
            }

            if (resolution <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(resolution), "Max timeout has to be a positive value.");
            }

            if (resolution * MinResolutionRatio > maxTimeout)
            {
                throw new ArgumentOutOfRangeException(nameof(resolution), $"The resolution has to be at most 1/{MinResolutionRatio}th of maxTimeout.");
            }

            _maxTimeout = maxTimeout;
            _minTimeout = resolution * MinResolutionRatio;
            _inverseResolution = 1d / resolution.Ticks;

            _pools = new PoolSegment[(int)Math.Ceiling((double)maxTimeout.Ticks / resolution.Ticks) - MinResolutionRatio + 1];
            for (var i = 0; i < _pools.Length; i++)
            {
                _pools[i] = new PoolSegment();
            }

            _defaultPool = _pools[^1];

            using (ExecutionContext.SuppressFlow())
            {
                _timer = new Timer(static s => ((TimeoutCtsPool)s!).TimerCallback(), this, resolution / 2, resolution / 2);
            }
        }

        private void TimerCallback()
        {
            foreach (var pool in _pools)
            {
                pool.Reset();
            }
        }

        public PooledCts Rent(TimeSpan timeout, CancellationToken linkedToken)
        {
            PooledCts cts;
            if (timeout == _maxTimeout)
            {
                cts = _defaultPool.Rent(timeout);
            }
            else if (timeout <= _maxTimeout && timeout >= _minTimeout)
            {
                cts = _pools[(int)(timeout.Ticks * _inverseResolution) - MinResolutionRatio].Rent(timeout);
            }
            else
            {
                cts = RentFromSharedPool(timeout);
            }

            cts.Register(linkedToken);
            return cts;
        }

        public void Dispose()
        {
            _timer.Dispose();
            foreach (var pool in _pools)
            {
                pool.Dispose();
            }
        }
    }
}
