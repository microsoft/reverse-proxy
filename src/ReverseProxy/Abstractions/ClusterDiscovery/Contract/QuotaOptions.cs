// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions
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

        internal static bool Equals(QuotaOptions options1, QuotaOptions options2)
        {
            if (options1 == null && options2 == null)
            {
                return true;
            }

            if (options1 == null || options2 == null)
            {
                return false;
            }

            return options1.Average == options2.Average
                && options1.Burst == options2.Burst;
        }
    }
}
