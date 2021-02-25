// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// State used when validating transforms for the given route.
    /// </summary>
    public class TransformRouteValidationContext
    {
        /// <summary>
        /// Application services that can be used to validate transforms.
        /// </summary>
        public IServiceProvider Services { get; init; }

        /// <summary>
        /// The route these transforms are associated with.
        /// </summary>
        public ProxyRoute Route { get; init; }

        /// <summary>
        /// The accumulated list of validation errors for this route.
        /// Add transform validation errors here.
        /// </summary>
        public IList<Exception> Errors { get; } = new List<Exception>();
    }
}
