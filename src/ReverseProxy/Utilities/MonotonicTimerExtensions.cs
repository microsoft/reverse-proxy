// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions.Time;

namespace Microsoft.ReverseProxy.Utilities
{
    /// <summary>
    /// Extension methods for <see cref="MonotonicTimer"/>.
    /// </summary>
    public static class MonotonicTimerExtensions
    {
        /// <summary>
        /// Creates a task that completes <paramref name="delay"/> after <see cref="IMonotonicTimer.CurrentTime"/>.
        /// </summary>
        /// <param name="timer"><see cref="IMonotonicTimer"/> instance.</param>
        /// <param name="delay">How much time to delay for.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>A task which completes when the delay has elapsed.</returns>
        public static async Task Delay(this IMonotonicTimer timer, TimeSpan delay, CancellationToken cancellation)
        {
            if (timer == null)
            {
                throw new ArgumentNullException(nameof(timer));
            }

            await timer.DelayUntil(timer.CurrentTime + delay, cancellation);
        }

        /// <summary>
        /// Operates like <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/> but supporting specifying a custom timer.
        /// </summary>
        /// <param name="cancellationTokenSource">Token to cancel after expiration is complete.</param>
        /// <param name="timeout">Timeout after which the cancellationTokenSource will be canceled.</param>
        /// <param name="timer">Timer to perform the measurement of time for determining when to cancel.</param>
        public static async void CancelAfter(this CancellationTokenSource cancellationTokenSource, TimeSpan timeout, IMonotonicTimer timer)
        {
            if (timer == null)
            {
                throw new ArgumentNullException(nameof(timer));
            }

            try
            {
                await timer.Delay(timeout, cancellationTokenSource.Token);
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore disposed cancellation tokens. Indicates cancellation is no longer needed. Unfortunately CTS's don't give a good
                // way to safely check async disposal, so must rely on exception handling instead.
            }
            catch (OperationCanceledException)
            {
                // It cts was canceled, then there's no need for us to cancel the token. Return successfully.
                // Note that we can't avoid this situation in advance as we strongly desire here to retain the 'void' returning
                // interface that cts.CancelAfter(ts) has.
            }
        }
    }
}
