// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Session affinitity options.
    /// </summary>
    public sealed class SessionAffinityOptions
    {
        /// <summary>
        /// Indicates whether session affinity is enabled.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Session affinity mode which is implemented by one of providers.
        /// </summary>
        public string Mode { get; set; }

        /// <summary>
        /// Strategy handling missing destination for an affinitized request.
        /// </summary>
        public string FailurePolicy { get; set; }

        /// <summary>
        /// Key-value pair collection holding extra settings specific to different affinity modes.
        /// </summary>
        public IDictionary<string, string> Settings { get; set; }

        internal SessionAffinityOptions DeepClone()
        {
            return new SessionAffinityOptions
            {
                Enabled = Enabled,
                Mode = Mode,
                FailurePolicy = FailurePolicy,
                Settings = Settings?.DeepClone(StringComparer.OrdinalIgnoreCase)
            };
        }

        internal static bool Equals(SessionAffinityOptions options1, SessionAffinityOptions options2)
        {
            if (options1 == null && options2 == null)
            {
                return true;
            }

            if (options1 == null || options2 == null)
            {
                return false;
            }

            return options1.Enabled == options2.Enabled
                && string.Equals(options1.Mode, options2.Mode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(options1.FailurePolicy, options2.FailurePolicy, StringComparison.OrdinalIgnoreCase)
                && CaseInsensitiveEqualHelper.Equals(options1.Settings, options2.Settings);
        }
    }
}
