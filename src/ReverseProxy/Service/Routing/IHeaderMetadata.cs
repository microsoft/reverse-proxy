// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Service.Routing
{
    /// <summary>
    /// Represents request header metadata used during routing.
    /// </summary>
    internal interface IHeaderMetadata
    {
        /// <summary>
        /// Name of the header to look for.
        /// </summary>
        string HeaderName { get; }

        /// <summary>
        /// Returns a read-only collection of acceptable header values used during routing.
        /// The list must not be empty.
        /// </summary>
        IReadOnlyList<string> HeaderValues { get; }

        /// <summary>
        /// Specifies how header values should be compared (e.g. exact matches Vs. by prefix).
        /// Defaults to <see cref="HeaderMatchMode.ExactHeader"/>.
        /// </summary>
        HeaderMatchMode Mode { get; }

        // Not implemented:
        // A request header may have multiple values, either as multiple headers,
        // a comma separated header, or some combination of the two.
        // Also don't forget cookies that are semi-colon separated.
        // The current implementation doesn't attempt to match individual header values,
        // it only supports matching a single full header.
        // bool AllowMultiValueHeaders { get; }

        /// <summary>
        /// Specifies whether header value comparisons should ignore case.
        /// When <c>true</c>, <see cref="StringComparison.Ordinal" /> is used.
        /// When <c>false</c>, <see cref="StringComparison.OrdinalIgnoreCase" /> is used.
        /// Defaults to <c>false</c>.
        /// </summary>
        bool CaseSensitive { get; }
    }
}
