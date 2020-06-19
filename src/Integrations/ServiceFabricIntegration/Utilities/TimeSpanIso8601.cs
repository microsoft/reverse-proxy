// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Xml;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration.Utilities
{
    /// <summary>
    /// Handles ISO 8601 timespans in an IConfigurationBuilder configuration.
    /// </summary>
    /// <remarks>
    /// ISO 8601 (also documented in RFC3339 to some degree) is the duration standard commonly used in Azure
    /// for encoding durations. For example, the azure VMSS autoscale settings (https://docs.microsoft.com/en-us/azure/azure-monitor/platform/autoscale-understanding-settings)
    /// use this format for specifying 'timeWindow' and 'timeGrain' parameters.
    /// </remarks>
    [TypeConverter(typeof(Converter))]
    public struct TimeSpanIso8601
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeSpanIso8601"/> struct.
        /// </summary>
        public TimeSpanIso8601(string iso8601Duration)
        {
            Value = XmlConvert.ToTimeSpan(iso8601Duration);
        }

        /// <summary>
        /// TODO.
        /// </summary>
        public TimeSpan Value { get; set; }

        /// <summary>
        /// TODO.
        /// </summary>
        public static implicit operator TimeSpan(TimeSpanIso8601 timeSpanIso8601) => timeSpanIso8601.Value;

        /// <summary>
        /// TODO.
        /// </summary>
        public static implicit operator TimeSpanIso8601(TimeSpan timeSpan) => new TimeSpanIso8601 { Value = timeSpan };

        /// <summary>
        /// TODO.
        /// </summary>
        public class Converter : TypeConverter
        {
            /// <inheritdoc />
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return sourceType == typeof(string);
            }

            /// <inheritdoc />
            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                return new TimeSpanIso8601((string)value);
            }
        }
    }
}
