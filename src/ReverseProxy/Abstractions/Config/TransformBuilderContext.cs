// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// State used when building transforms for the given route.
    /// </summary>
    public class TransformBuilderContext
    {
        /// <summary>
        /// Application services that can be used to construct transforms.
        /// </summary>
        public IServiceProvider Services { get; init; }

        /// <summary>
        /// The route these transforms will be associated with.
        /// </summary>
        public ProxyRoute Route { get; init; }

        /// <summary>
        /// Indicates if request headers should all be copied to the proxy request before transforms are applied.
        /// </summary>
        public bool? CopyRequestHeaders { get; init; }

        /// <summary>
        /// Indicates if response headers should all be copied to the client response before transforms are applied.
        /// </summary>
        public bool? CopyResponseHeaders { get; init; }

        /// <summary>
        /// Indicates if response trailers should all be copied to the client response before transforms are applied.
        /// </summary>
        public bool? CopyResponseTrailers { get; init; }

        /// <summary>
        /// Indicates if the proxy request should use the host header from the client request or from the destination url.
        /// </summary>
        public bool? UseOriginalHost { get; init; }

        /// <summary>
        /// Indicates if default x-fowarded-* transforms should be added to this route. Disable this if you do not want
        /// x-forwarded-* headers or have configured your own.
        /// </summary>
        public bool? UseDefaultForwarders { get; init; }

        /// <summary>
        /// Add request transforms here for the given route.
        /// </summary>
        public IList<RequestTransform> RequestTransforms { get; init; }

        /// <summary>
        /// Add response transforms here for the given route.
        /// </summary>
        public IList<ResponseTransform> ResponseTransforms { get; init; }

        /// <summary>
        /// Add response trailers transforms here for the given route.
        /// </summary>
        public IList<ResponseTrailersTransform> ResponseTrailersTransforms { get; init; }
    }
}
