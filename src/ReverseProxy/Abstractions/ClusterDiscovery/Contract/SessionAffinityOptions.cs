// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Abstractions
{
    /// <summary>
    /// Session affinity options.
    /// </summary>
    public sealed record SessionAffinityOptions
    {
        /// <summary>
        /// Indicates whether session affinity is enabled.
        /// </summary>
        public bool? Enabled { get; init; }

        /// <summary>
        /// Session affinity mode which is implemented by one of providers.
        /// </summary>
        public string Mode { get; init; }

        /// <summary>
        /// Strategy handling missing destination for an affinitized request.
        /// </summary>
        public string FailurePolicy { get; init; }

        /// <summary>
        /// Key-value pair collection holding extra settings specific to different affinity modes.
        /// </summary>
        public IReadOnlyDictionary<string, string> Settings { get; init; }

        /// <inheritdoc />
        public bool Equals(SessionAffinityOptions other)
        {
            if (other == null)
            {
                return false;
            }

            return Enabled == other.Enabled
                && string.Equals(Mode, other.Mode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(FailurePolicy, other.FailurePolicy, StringComparison.OrdinalIgnoreCase)
                && CaseInsensitiveEqualHelper.Equals(Settings, other.Settings);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Enabled,
                Mode?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                FailurePolicy?.GetHashCode(StringComparison.OrdinalIgnoreCase),
                Settings);
        }
    }
}
