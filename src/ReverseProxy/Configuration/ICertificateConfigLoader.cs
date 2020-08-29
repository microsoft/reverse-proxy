// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;
using Microsoft.ReverseProxy.Configuration.Contract;

namespace Microsoft.ReverseProxy.Configuration
{
    /// <summary>
    /// Loads an <see cref="X509Certificate2"/> specified in a <see cref="CertificateConfigOptions"/>.
    /// Supports various certificate configuration schemas such as path and password, store and subject, and PEM format (on .NET version 5.0 or higher).
    /// </summary>
    internal interface ICertificateConfigLoader
    {
        /// <summary>
        /// Loads the certificate specified by <paramref name="certificateConfig"/>.
        /// </summary>
        /// <param name="clusterId"><see cref="Cluster"/>'s ID.</param>
        /// <param name="certificateConfig">Certificate configuration.</param>
        /// <returns>An <see cref="X509Certificate2"/> instance if loading completed successfully; otherwise null.</returns>
        X509Certificate2 LoadCertificate(string clusterId, CertificateConfigOptions certificateConfig);
    }
}
