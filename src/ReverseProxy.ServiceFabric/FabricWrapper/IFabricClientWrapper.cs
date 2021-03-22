// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Fabric;

namespace Yarp.ReverseProxy.ServiceFabric
{
    internal interface IFabricClientWrapper
    {
        FabricClient FabricClient { get; }
    }
}
