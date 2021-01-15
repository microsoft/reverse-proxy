// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Fabric.Query;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// TODO .
    /// </summary>
    internal class ServiceWrapper
    {
        public Uri ServiceName { get; set; }

        public string ServiceTypeName { get; set; }

        public string ServiceManifestVersion { get; set; }

        public ServiceKind ServiceKind { get; set; }
    }
}
