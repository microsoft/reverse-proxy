// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using System;

namespace IngressController.Caching
{
    /// <summary>
    /// Holds data needed from a <see cref="V1Ingress"/> resource.
    /// </summary>
    public struct IngressData
    {
        public IngressData(V1Ingress ingress)
        {
            if (ingress is null)
            {
                throw new ArgumentNullException(nameof(ingress));
            }

            Spec = ingress.Spec;
            Metadata = ingress.Metadata;
        }

        public V1IngressSpec Spec { get; set; }
        public V1ObjectMeta Metadata { get; set; }
    }
}
