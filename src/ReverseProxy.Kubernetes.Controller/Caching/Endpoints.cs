// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Kubernetes.Controller.Caching
{
    /// <summary>
    /// Holds data needed from a <see cref="V1Endpoints"/> resource.
    /// </summary>
    public struct Endpoints
    {
        public Endpoints(V1Endpoints endpoints)
        {
            if (endpoints is null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            Name = endpoints.Name();
            Subsets = endpoints.Subsets;
        }

        public string Name { get; set; }
        public IList<V1EndpointSubset> Subsets { get; set; }
    }
}
