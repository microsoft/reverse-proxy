// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Cluster partitioning configures which endpoints in a cluster are eligible to process each request.
    /// </summary>
    public sealed class ClusterPartitioningOptions
    {
        /// <summary>
        /// Number of partitions.
        /// </summary>
        public int PartitionCount { get; set; }

        /// <summary>
        /// Describes how to compute the partitioning key from an incoming request.
        /// E.g. <c>Header('x-ms-organization-id')</c>.
        /// </summary>
        public string PartitionKeyExtractor { get; set; }

        /// <summary>
        /// E.g. "SHA256".
        /// </summary>
        public string PartitioningAlgorithm { get; set; }

        internal ClusterPartitioningOptions DeepClone()
        {
            return new ClusterPartitioningOptions
            {
                PartitionCount = PartitionCount,
                PartitionKeyExtractor = PartitionKeyExtractor,
                PartitioningAlgorithm = PartitioningAlgorithm,
            };
        }

        internal static bool Equals(ClusterPartitioningOptions options1, ClusterPartitioningOptions options2)
        {
            if (options1 == null && options2 == null)
            {
                return true;
            }

            if (options1 == null || options2 == null)
            {
                return false;
            }

            return options1.PartitionCount == options2.PartitionCount
                && string.Equals(options1.PartitionKeyExtractor, options2.PartitionKeyExtractor, StringComparison.OrdinalIgnoreCase)
                && string.Equals(options1.PartitioningAlgorithm, options2.PartitioningAlgorithm, StringComparison.OrdinalIgnoreCase);
        }
    }
}
