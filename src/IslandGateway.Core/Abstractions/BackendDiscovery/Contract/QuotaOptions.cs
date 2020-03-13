// <copyright file="QuotaOptions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// Quota / throttling options.
    /// </summary>
    public sealed class QuotaOptions
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

        internal QuotaOptions DeepClone()
        {
            return new QuotaOptions
            {
                Average = Average,
                Burst = Burst,
            };
        }
    }
}
