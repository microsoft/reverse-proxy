// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Dispatching;

/// <summary>
/// IDispatcher is a service interface to bridge outgoing data to the
/// current connections.
/// </summary>
public interface IDispatcher
{
    Task AttachAsync(IDispatchTarget target, CancellationToken cancellationToken);
    void Detach(IDispatchTarget target);
    Task SendAsync(byte[] utf8Bytes, CancellationToken cancellationToken);
}
