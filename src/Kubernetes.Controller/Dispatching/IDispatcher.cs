// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Dispatching
{
    /// <summary>
    /// IDispatcher is a service interface to bridge outgoing data to the
    /// current connections.
    /// </summary>
    public interface IDispatcher
    {
        void Attach(IDispatchTarget target);
        void Detach(IDispatchTarget target);
        void OnAttach(Action<IDispatchTarget> attached);
        Task SendAsync(IDispatchTarget specificTarget, byte[] utf8Bytes, CancellationToken cancellationToken);
    }
}
