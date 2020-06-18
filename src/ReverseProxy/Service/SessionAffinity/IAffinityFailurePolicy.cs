// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Middleware;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    /// <summary>
    /// Affinity failures handling policy.
    /// </summary>
    internal interface IAffinityFailurePolicy
    {
        /// <summary>
        ///  A unique identifier for this failure policy. This will be referenced from config.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Handles affinity failures. This method assumes the full control on <see cref="HttpContext"/>
        /// and can change it in any way.
        /// </summary>
        /// <param name="context">Current request's context.</param>
        /// <param name="options">Session affinity options set for the cluster.</param>
        /// <param name="affinityStatus">Affinity resolution status.</param>
        /// <returns>
        /// <see cref="true"/> if the failure is considered recoverable and the request processing can proceed.
        /// Otherwise, <see cref="false"/> indicating that an error response has been generated and the request's processing must be terminated.
        /// </returns>
        public Task<bool> Handle(HttpContext context, ClusterConfig.ClusterSessionAffinityOptions options, AffinityStatus affinityStatus);
    }
}
