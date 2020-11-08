// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Security.Authentication;

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    public sealed class ProxyHttpClientData
    {
        public List<SslProtocols> SslProtocols { get; set; }

        public bool DangerousAcceptAnyServerCertificate { get; set; }

        public CertificateConfigData ClientCertificate { get; set; }

        public int? MaxConnectionsPerServer { get; set; }

        public bool? PropagateActivityContext { get; set; }

        // TODO: Add this property once we have migrated to SDK version that supports it.
        //public bool? EnableMultipleHttp2Connections { get; set; }
    }
}
