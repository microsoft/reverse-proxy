// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Fabric;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    internal class FabricClientWrapper : IFabricClientWrapper, IDisposable
    {
        public FabricClientWrapper()
        {
            FabricClient = new FabricClient();
        }

        public FabricClient FabricClient { get; }

        public void Dispose()
        {
            FabricClient.Dispose();
        }
    }
}
