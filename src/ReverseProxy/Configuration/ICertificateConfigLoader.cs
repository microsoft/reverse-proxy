// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

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
        /// <param name="certificateConfig">Certificate configuration.</param>
        /// <returns>An <see cref="X509Certificate2"/> instance if loading completed successfully.
        /// It never returns null, but throws an exception in case of a failure.</returns>
        X509Certificate2 LoadCertificate(IConfigurationSection certificateConfig);
    }
}
