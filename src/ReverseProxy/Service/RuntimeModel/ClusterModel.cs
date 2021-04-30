// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Yarp.ReverseProxy.Abstractions;

namespace Yarp.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a cluster
    /// that only change in reaction to configuration changes
    /// (e.g. http client options).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="ClusterModel"/> are replaced
    /// in their entirety when values need to change.
    /// </remarks>
    public sealed class ClusterModel
    {
        public ClusterModel(
            Cluster cluster,
            HttpMessageInvoker httpClient)
        {
            Options = cluster ?? throw new ArgumentNullException(nameof(cluster));
            HttpClient = httpClient;
        }

        public Cluster Options { get; }

        /// <summary>
        /// An <see cref="HttpMessageInvoker"/> that used for proxying requests to an upstream server.
        /// </summary>
        public HttpMessageInvoker HttpClient { get; }

        // We intentionally do not consider destination changes when updating the cluster Revision.
        // Revision is used to rebuild routing endpoints which should be unrelated to destinations,
        // and destinations are the most likely to change.
        internal bool HasConfigChanged(ClusterModel newModel)
        {
            return !Options.EqualsExcludingDestinations(newModel.Options) || newModel.HttpClient != HttpClient;
        }
    }
}
