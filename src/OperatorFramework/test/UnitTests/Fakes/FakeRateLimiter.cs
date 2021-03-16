// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Controller.RateLimiters;
using System;

namespace Microsoft.Kubernetes.Fakes
{
    public class FakeRateLimiter<TItem> : IRateLimiter<TItem>
    {
        public Action<TItem> OnForget { get; set; } = _ => { };
        public Func<TItem, int> OnNumRequeues { get; set; } = _ => 0;
        public Func<TItem, TimeSpan> OnItemDelay { get; set; } = _ => default;

        public void Forget(TItem item) => OnForget(item);

        public int NumRequeues(TItem item) => OnNumRequeues(item);

        public TimeSpan ItemDelay(TItem item) => OnItemDelay(item);
    }
}
