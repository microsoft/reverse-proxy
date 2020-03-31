// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;

namespace Tests.Common
{
    /// <summary>
    /// Implementation of <see cref="IMetricCreator"/>
    /// which doesn't log anything.
    /// </summary>
    public class TestMetricCreator : IMetricCreator
    {
        public List<string> MetricsLogged { get; } = new List<string>();

        /// <inheritdoc/>
        public Action<long> Create(string metricName)
        {
            var slowLogger = Create(metricName, new string[0]);
            return (long value) => slowLogger(value, new string[0]);
        }

        /// <inheritdoc/>
        public Action<long, string> Create(string metricName, string dimensionName)
        {
            var slowLogger = Create(metricName, new[] { dimensionName });
            return (long value, string dimension1) => slowLogger(value, new[] { dimension1 });
        }

        /// <inheritdoc/>
        public Action<long, string, string> Create(string metricName, string dimensionName1, string dimensionName2)
        {
            var slowLogger = Create(metricName, new[] { dimensionName1, dimensionName2 });
            return (long value, string dimension1, string dimension2) => slowLogger(value, new[] { dimension1, dimension2 });
        }

        /// <inheritdoc/>
        public Action<long, string, string, string> Create(string metricName, string dimensionName1, string dimensionName2, string dimensionName3)
        {
            var slowLogger = Create(metricName, new[] { dimensionName1, dimensionName2, dimensionName3 });
            return (long value, string dimension1, string dimension2, string dimension3) => slowLogger(value, new[] { dimension1, dimension2, dimension3 });
        }

        /// <inheritdoc/>
        public Action<long, string, string, string, string> Create(string metricName, string dimensionName1, string dimensionName2, string dimensionName3, string dimensionName4)
        {
            var slowLogger = Create(metricName, new[] { dimensionName1, dimensionName2, dimensionName3, dimensionName4 });
            return (long value, string dimension1, string dimension2, string dimension3, string dimension4) => slowLogger(value, new[] { dimension1, dimension2, dimension3, dimension4 });
        }

        /// <inheritdoc/>
        public Action<long, string, string, string, string, string> Create(string metricName, string dimensionName1, string dimensionName2, string dimensionName3, string dimensionName4, string dimensionName5)
        {
            var slowLogger = Create(metricName, new[] { dimensionName1, dimensionName2, dimensionName3, dimensionName4, dimensionName5 });
            return (long value, string dimension1, string dimension2, string dimension3, string dimension4, string dimension5) => slowLogger(value, new[] { dimension1, dimension2, dimension3, dimension4, dimension5 });
        }

        /// <inheritdoc/>
        public Action<long, string[]> Create(string metricName, params string[] dimensionNames)
        {
            // Produces results that look like the following:
            // "metricName=value;"
            // "metricName=value;dim1=dimValue1"
            // "metricName=value;dim1=dimValue1;dim2=dimValue2"
            // ...
            return (long value, string[] dimensions) => MetricsLogged.Add($"{metricName}={value};{string.Join(";", dimensionNames.Zip(dimensions).Select((ValueTuple<string, string> pair) => $"{pair.Item1}={pair.Item2}"))}");
        }
    }
}
