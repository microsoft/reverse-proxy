// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// All health check options.
    /// </summary>
    public sealed class HealthCheckData
    {
        /// <summary>
        /// Passive health check options.
        /// </summary>
        public PassiveHealthCheckData Passive { get; set; }

        /// <summary>
        /// Active health check options.
        /// </summary>
        public ActiveHealthCheckData Active { get; set; }
    }
}
