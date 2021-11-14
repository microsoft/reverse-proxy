// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Controller.Queues;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Fakes;

public sealed class FakeQueue<TItem> : IRateLimitingQueue<TItem>
{
    public FakeQueue()
    {
        var shutdown = false;
        OnShutdown = () => shutdown = true;
        OnShuttingDown = () => shutdown;
    }

    public Action<TItem> OnAdd { get; set; } = (_) => { };
    public Action<TItem, TimeSpan> OnAddAfter { get; set; } = (_, _) => { };
    public Action OnDispose { get; set; } = () => { };
    public Action<TItem> OnDone { get; set; } = _ => { };
    public Func<CancellationToken, Task<(TItem item, bool shutdown)>> OnGetAsync { get; set; } = _ => Task.FromResult((default(TItem), true));
    public Func<int> OnLen { get; set; } = () => 0;
    public Action OnShutdown { get; set; }
    public Func<bool> OnShuttingDown { get; set; }
    public Action<TItem> OnAddRateLimited { get; set; } = _ => { };
    public Action<TItem> OnForget { get; set; } = _ => { };
    public Func<TItem, int> OnNumRequeues { get; set; } = _ => 0;

    public void Add(TItem item) => OnAdd(item);

    public void AddAfter(TItem item, TimeSpan delay) => OnAddAfter(item, delay);

    public void AddRateLimited(TItem item) => OnAddRateLimited(item);

    public void Dispose() => OnDispose();

    public void Done(TItem item) => OnDone(item);

    public void Forget(TItem item) => OnForget(item);

    public Task<(TItem item, bool shutdown)> GetAsync(CancellationToken cancellationToken) => OnGetAsync(cancellationToken);

    public int Len() => OnLen();

    public int NumRequeues(TItem item) => OnNumRequeues(item);

    public void ShutDown() => OnShutdown();

    public bool ShuttingDown() => OnShuttingDown();
}
