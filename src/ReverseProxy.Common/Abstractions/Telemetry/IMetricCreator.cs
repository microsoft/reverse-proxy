// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Common.Abstractions.Telemetry
{
    /// <summary>
    /// Interface used to create new metrics.
    /// </summary>
    /// <remarks>
    /// This is modeled after the <c>Microsoft.Extensions.Logging.LoggerMessage</c> pattern.
    /// See <seealso href="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/loggermessage?view=aspnetcore-3.1"/>
    /// for more info.
    /// </remarks>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// // Registered as a singleton
    /// internal class MyServiceSingletonMetrics
    /// {
    ///     private readonly Action<long, string, string> someMetric;
    ///
    ///     public MyServiceSingletonMetrics(IMetricCreator metricCreator)
    ///     {
    ///         this.someMetric = metricCreator.Create("SomeMetric", "foo", "bar");
    ///     }
    ///
    ///     public void SomeMetric(long value, string foo, string bar)
    ///     {
    ///         this.someMetric(value, foo, bar);
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public interface IMetricCreator
    {
        /// <summary>
        /// Creates a new metric with the provided name and no additional dimensions.
        /// </summary>
        Action<long> Create(string metricName);

        /// <summary>
        /// Creates a new metric with the provided name and 1 additional dimension.
        /// </summary>
        Action<long, string> Create(string metricName, string dimensionName);

        /// <summary>
        /// Creates a new metric with the provided name and 2 additional dimensions.
        /// </summary>
        Action<long, string, string> Create(string metricName, string dimensionName1, string dimensionName2);

        /// <summary>
        /// Creates a new metric with the provided name and 3 additional dimensions.
        /// </summary>
        Action<long, string, string, string> Create(string metricName, string dimensionName1, string dimensionName2, string dimensionName3);

        /// <summary>
        /// Creates a new metric with the provided name and 4 additional dimensions.
        /// </summary>
        Action<long, string, string, string, string> Create(string metricName, string dimensionName1, string dimensionName2, string dimensionName3, string dimensionName4);

        /// <summary>
        /// Creates a new metric with the provided name and 5 additional dimensions.
        /// </summary>
        Action<long, string, string, string, string, string> Create(string metricName, string dimensionName1, string dimensionName2, string dimensionName3, string dimensionName4, string dimensionName5);

        /// <summary>
        /// Creates a new metric with the provided name and an arbitrary number of additional dimensions.
        /// </summary>
        Action<long, string[]> Create(string metricName, params string[] dimensionNames);
    }
}
