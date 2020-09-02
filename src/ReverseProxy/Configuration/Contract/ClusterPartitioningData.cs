// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// Cluster partitioning configures which endpoints in a cluster are eligible to process each request.
    /// </summary>
    public sealed class ClusterPartitioningData
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
    }
}
