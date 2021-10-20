// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using System;

namespace Yarp.Kubernetes.Controller.Caching
{
    /// <summary>
    /// Holds data needed from a <see cref="V1Service"/> resource.
    /// </summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct ServiceData
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public ServiceData(V1Service service)
        {
            if (service is null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            Spec = service.Spec;
            Metadata = service.Metadata;
        }

        public V1ServiceSpec Spec { get; set; }
        public V1ObjectMeta Metadata { get; set; }
    }
}
