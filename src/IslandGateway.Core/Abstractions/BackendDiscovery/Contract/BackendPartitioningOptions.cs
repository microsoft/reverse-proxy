// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// Backend partitioning configures which endpoints in a backend are eligible to process each request.
    /// </summary>
    public sealed class BackendPartitioningOptions
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

        internal BackendPartitioningOptions DeepClone()
        {
            return new BackendPartitioningOptions
            {
                PartitionCount = PartitionCount,
                PartitionKeyExtractor = PartitionKeyExtractor,
                PartitioningAlgorithm = PartitioningAlgorithm,
            };
        }
    }
}
