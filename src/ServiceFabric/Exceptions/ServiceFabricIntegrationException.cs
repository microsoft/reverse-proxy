// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.ServiceFabric;

/// <summary>
/// Represents errors related to Service Fabric integration with the gateway.
/// </summary>
public sealed class ServiceFabricIntegrationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceFabricIntegrationException"/> class.
    /// </summary>
    public ServiceFabricIntegrationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceFabricIntegrationException"/> class.
    /// </summary>
    public ServiceFabricIntegrationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceFabricIntegrationException"/> class.
    /// </summary>
    public ServiceFabricIntegrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
