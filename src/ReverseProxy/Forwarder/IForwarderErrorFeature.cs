// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Forwarder;

/// <summary>
/// Stores errors and exceptions that occurred when forwarding the request to the destination.
/// </summary>
public interface IForwarderErrorFeature
{
    /// <summary>
    /// The specified ProxyError.
    /// </summary>
    ForwarderError Error { get; }

    /// <summary>
    /// An Exception that occurred when forwarding the request to the destination, if any.
    /// </summary>
    Exception? Exception { get; }
}
