// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// How to match Path Parameter values.
/// </summary>
public enum PathParameterMatchMode
{
    /// <summary>
    /// Path parameter value must match in its entirety,
    /// Subject to case sensitivity settings.
    /// </summary>
    Exact,

    /// <summary>
    /// Path parameter value substring must match for each of the respective provided values.
    /// Subject to case sensitivity settings.
    /// </summary>
    Contains,

    /// <summary>
    /// Path parameter value substring must not match for any of the respective provided values.
    /// Subject to case sensitivity settings.
    /// </summary>
    NotContains,

    /// <summary>
    /// Path parameter value prefix must match for at least one of the respective provided values.
    /// Subject to case sensitivity settings.
    /// </summary>
    Prefix,
}
