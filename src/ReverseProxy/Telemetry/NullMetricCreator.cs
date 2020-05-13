// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Abstractions.Telemetry;

namespace Microsoft.ReverseProxy.Telemetry
{
    /// <summary>
    /// Implementation of <see cref="IMetricCreator"/>
    /// which doesn't log anything.
    /// </summary>
    public class NullMetricCreator : IMetricCreator
    {
        /// <inheritdoc/>
        public Action<long> Create(string metricName)
        {
            return (long value) => { };
        }

        /// <inheritdoc/>
        public Action<long, string> Create(string metricName, string dimensionName)
        {
            return (long value, string dimension1) => { };
        }

        /// <inheritdoc/>
        public Action<long, string, string> Create(string metricName, string dimensionName1, string dimensionName2)
        {
            return (long value, string dimension1, string dimension2) => { };
        }

        /// <inheritdoc/>
        public Action<long, string, string, string> Create(string metricName, string dimensionName1, string dimensionName2, string dimensionName3)
        {
            return (long value, string dimension1, string dimension2, string dimension3) => { };
        }

        /// <inheritdoc/>
        public Action<long, string, string, string, string> Create(string metricName, string dimensionName1, string dimensionName2, string dimensionName3, string dimensionName4)
        {
            return (long value, string dimension1, string dimension2, string dimension3, string dimension4) => { };
        }

        /// <inheritdoc/>
        public Action<long, string, string, string, string, string> Create(string metricName, string dimensionName1, string dimensionName2, string dimensionName3, string dimensionName4, string dimensionName5)
        {
            return (long value, string dimension1, string dimension2, string dimension3, string dimension4, string dimension5) => { };
        }

        /// <inheritdoc/>
        public Action<long, string[]> Create(string metricName, params string[] dimensionNames)
        {
            var expectedDimensions = dimensionNames.Length;
            return (long value, string[] dimensions) =>
            {
                if (dimensions == null || dimensions.Length != expectedDimensions)
                {
                    throw new ArgumentException($"Expected {expectedDimensions} dimensions.", nameof(dimensions));
                }
            };
        }
    }
}
