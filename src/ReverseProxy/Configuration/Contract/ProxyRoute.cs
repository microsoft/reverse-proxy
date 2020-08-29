// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// Describes a route that matches incoming requests based on a the <see cref="Match"/> criteria
    /// and proxies matching requests to the cluster identified by its <see cref="ClusterId"/>.
    /// </summary>
    public sealed class ProxyRoute
    {
        /// <summary>
        /// Globally unique identifier of the route.
        /// </summary>
        public string RouteId { get; set; }

        public ProxyMatch Match { get; private set; } = new ProxyMatch();

        /// <summary>
        /// Optionally, a priority value for this route. Routes with lower numbers take precedence over higher numbers.
        /// </summary>
        public int? Priority { get; set; }

        /// <summary>
        /// Gets or sets the cluster that requests matching this route
        /// should be proxied to.
        /// </summary>
        public string ClusterId { get; set; }

        /// <summary>
        /// The name of the AuthorizationPolicy to apply to this route.
        /// If not set then only the FallbackPolicy will apply.
        /// Set to "Default" to enable authorization with the applications default policy.
        /// </summary>
        public string AuthorizationPolicy { get; set; }

        /// <summary>
        /// The name of the CorsPolicy to apply to this route.
        /// If not set then the route won't be automatically matched for cors preflight requests.
        /// Set to "Default" to enable cors with the default policy.
        /// Set to "Disable" to refuses cors requests for this route.
        /// </summary>
        public string CorsPolicy { get; set; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this route.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// Parameters used to transform the request and response. See <see cref="Service.ITransformBuilder"/>.
        /// </summary>
        public IList<IDictionary<string, string>> Transforms { get; set; }
    }
}
