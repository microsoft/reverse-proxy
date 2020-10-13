// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Active health check evaulation policy.
    /// </summary>
    public interface IActiveHealthCheckPolicy
    {
        /// <summary>
        /// Policy's name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Registers a successful or failed active health probing result and evaluates a new <see cref="CompositeDestinationHealth.Active"/> value.
        /// </summary>
        /// <param name="cluster">Cluster.</param>
        /// <param name="destination">Probed destination.</param>
        /// <param name="response">Response to a probe.</param>
        /// <param name="error">Error occurred in the course of a probing, if any. Otherwise, null.</param>
        void ProbingCompleted(ClusterConfig cluster, DestinationInfo destination, HttpResponseMessage response, Exception exception);
    }
}
