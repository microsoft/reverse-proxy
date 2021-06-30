// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// State used when validating transforms for the given cluster.
    /// </summary>
    public class TransformClusterValidationContext
    {
        /// <summary>
        /// Application services that can be used to validate transforms.
        /// </summary>
        public IServiceProvider Services { get; init; }

        /// <summary>
        /// The cluster configuration that may be used when creating transforms.
        /// </summary>
        public Cluster Cluster { get; init; }

        /// <summary>
        /// The accumulated list of validation errors for this cluster.
        /// Add validation errors here.
        /// </summary>
        public IList<Exception> Errors { get; } = new List<Exception>();
    }
}
