// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Configuration;

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
    /// The header must match by contains, subject to case sensitivity settings.
    /// Only single headers are supported. If there are multiple headers with the same name then the match fails.
    /// </summary>
    Contains,

    /// <summary>
    /// The header name must exist and the value must be non-empty and not match, subject to case sensitivity settings.
    /// If there are multiple values then it needs to not contain ANY of the values 
    /// Only single headers are supported. If there are multiple headers with the same name then the match fails.
    /// </summary>
    NotContains,

    /// <summary>
    /// The header must exist and contain any non-empty value.
    /// </summary>
    Exists,

    /// <summary>
    /// The header must not exist.
    /// </summary>
    NotExists,
}
