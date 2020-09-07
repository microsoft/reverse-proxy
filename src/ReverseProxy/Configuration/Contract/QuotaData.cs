// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// Quota / throttling options.
    /// </summary>
    public sealed class QuotaData
    {
        /// <summary>
        /// Average allowed in a time window.
        /// </summary>
        // TODO: Define how time windows are computed.
        public double Average { get; set; }

        /// <summary>
        /// Burst allowance.
        /// </summary>
        public double Burst { get; set; }
    }
}
