// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Kubernetes;
using Yarp.Kubernetes.Controller.Converters;
using Yarp.Kubernetes.Controller.Dispatching;
using Yarp.Kubernetes.Protocol;

namespace Yarp.Kubernetes.Controller.Services;

/// <summary>
/// IReconciler is a service interface called by the <see cref="IngressController"/> to process
/// the work items as they are dequeued.
/// </summary>
public partial class Reconciler : IReconciler
{
    private readonly IDispatcher _dispatcher;
    private Action<IDispatchTarget> _attached;
    private readonly ILogger<Reconciler> _logger;

    public Reconciler(IDispatcher dispatcher, ILogger<Reconciler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _dispatcher.OnAttach(Attached);
        _logger = logger;
    }

    public void OnAttach(Action<IDispatchTarget> attached)
    {
        _attached = attached;
    }

    private void Attached(IDispatchTarget target)
    {
        _attached?.Invoke(target);
    }

    public async Task ProcessAsync(IDispatchTarget target, NamespacedName key, ReconcileData data, CancellationToken cancellationToken)
    {
        try
        {
            var message = new Message
            {
                MessageType = MessageType.Update,
                Key = $"{key.Namespace}:{key.Name}"
            };
            var context = new YarpIngressContext(data.Ingress, data.ServiceList, data.EndpointsList);
            YarpParser.CovertFromKubernetesIngress(context);

            message.Cluster = context.Clusters;
            message.Routes = context.Routes;

            var bytes = JsonSerializer.SerializeToUtf8Bytes(message);

            _logger.LogInformation(JsonSerializer.Serialize(message));

            await _dispatcher.SendAsync(target, bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);
            throw;
        }
    }
}
