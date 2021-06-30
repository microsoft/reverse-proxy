// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.Service.Routing;

namespace Yarp.ReverseProxy.Abstractions
{
    /// <summary>
    /// How to match header values.
    /// </summary>
    public enum HeaderMatchMode
    {
        /// <summary>
        /// The header must match in its entirety, subject to case sensitivity settings.
        /// Only single headers are supported. If there are multiple headers with the same name then the match fails.
        /// </summary>
        ExactHeader,

        // TODO: Matches individual values from multi-value headers (split by coma, or semicolon for cookies).
        // Also supports multiple headers of the same name.
        // ExactValue,
        // ValuePrefix,

        /// <summary>
        /// The header must match by prefix, subject to case sensitivity settings.
        /// Only single headers are supported. If there are multiple headers with the same name then the match fails.
        /// </summary>
        HeaderPrefix,

        /// <summary>
        /// The header must exist and contain any non-empty value.
        /// </summary>
        Exists,
    }
}
