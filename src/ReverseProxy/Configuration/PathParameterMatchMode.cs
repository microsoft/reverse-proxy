// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// How to match Path Parameter values.
/// </summary>
public enum PathParameterMatchMode
{
    /// <summary>
    /// Path parameter value prefix must match for at least one of the respective provided values.
    /// Subject to case sensitivity settings.
    /// </summary>
    Prefix,

    /// <summary>
    /// Path parameter value prefix must not match any of the respective provided values.
    /// Subject to case sensitivity settings.
    /// </summary>
    NotPrefix,
}
