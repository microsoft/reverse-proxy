// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract
{
    /// <summary>
    /// SSL certificate configuration.
    /// </summary>
    public class CertificateConfigOptions
    {
        public string Path { get; set; }

        public string KeyPath { get; set; }

        public string Password { get; set; }

        public string Subject { get; set; }

        public string Store { get; set; }

        public string Location { get; set; }

        public bool? AllowInvalid { get; set; }

        public bool IsFileCert => !string.IsNullOrEmpty(Path);

        public bool IsStoreCert => !string.IsNullOrEmpty(Subject);
    }
}
