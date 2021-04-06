// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Yarp.ReverseProxy.Abstractions;

namespace Yarp.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of a cluster
    /// that only change in reaction to configuration changes
    /// (e.g. health check options).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="ClusterConfig"/> are replaced
    /// in their entirety when values need to change.
    /// </remarks>
    public sealed class ClusterConfig
    {
        private readonly Dictionary<Type, object> _settings;

        public ClusterConfig(
            Cluster cluster,
            HttpMessageInvoker httpClient)
        {
            Options = cluster ?? throw new ArgumentNullException(nameof(cluster));
            HttpClient = httpClient;
        }

        public ClusterConfig(
            Cluster cluster,
            HttpMessageInvoker httpClient,
            IEnumerable<KeyValuePair<Type, object>> settings)
            : this(cluster,httpClient)
        {
            if (_settings != null)
            {
                _settings = new Dictionary<Type, object>(settings);
            }
        }

        public Cluster Options { get; }

        public T GetSettings<T>() => _settings != null && _settings.TryGetValue(typeof(T), out var value) ? (T)value : default;

        /// <summary>
        /// An <see cref="HttpMessageInvoker"/> that used for proxying requests to an upstream server.
        /// </summary>
        public HttpMessageInvoker HttpClient { get; }

        internal bool HasConfigChanged(ClusterConfig newClusterConfig)
        {
            return !Options.Equals(newClusterConfig.Options);
        }
    }
}
