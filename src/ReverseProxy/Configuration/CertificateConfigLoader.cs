// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Microsoft.ReverseProxy.Configuration
{
    /// <inheritdoc/>
    internal class CertificateConfigLoader : ICertificateConfigLoader
    {
        private readonly IWebHostEnvironment _hostEnvironment;

        public CertificateConfigLoader(IWebHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        }

        /// <inheritdoc/>
        public X509Certificate2 LoadCertificate(IConfigurationSection certificateSection)
        {
            if (certificateSection?.Exists() is false)
            {
                return null;
            }

            var certificateConfig = new CertificateConfigData
            {
                AllowInvalid = bool.TryParse(certificateSection[nameof(CertificateConfigData.AllowInvalid)], out var allowInvalid) ? allowInvalid : default,
                KeyPath = certificateSection[nameof(CertificateConfigData.KeyPath)],
                Location = certificateSection[nameof(CertificateConfigData.Location)],
                Password = certificateSection[nameof(CertificateConfigData.Password)],
                Path = certificateSection[nameof(CertificateConfigData.Path)],
                Store = certificateSection[nameof(CertificateConfigData.Store)],
                Subject = certificateSection[nameof(CertificateConfigData.Subject)],
            };


            if (certificateConfig.IsFileCert && certificateConfig.IsStoreCert)
            {
                throw new InvalidOperationException($"Multiple certificate sources are defined in the cluster configuration.");
            }
            else if (certificateConfig.IsFileCert)
            {
                var certificatePath = Path.Combine(_hostEnvironment.ContentRootPath, certificateConfig.Path);
                if (certificateConfig.KeyPath == null)
                {
                    return new X509Certificate2(Path.Combine(_hostEnvironment.ContentRootPath, certificateConfig.Path), certificateConfig.Password);
                }
                else
                {
#if NET5_0
                    return LoadPEMCertificate(certificateConfig, certificatePath);
#elif NETCOREAPP3_1
                    throw new NotSupportedException("PEM certificate format is only supported on .NET 5 or higher.");
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
                }
            }
            else if (certificateConfig.IsStoreCert)
            {
                return LoadFromCertStore(certificateConfig);
            }

            throw new ArgumentException($"Passed {nameof(CertificateConfigData)} doesn't define a certificate in any known format.");
        }

#if NET5_0
        private X509Certificate2 LoadPEMCertificate(CertificateConfigData certificateConfig, string certificatePath)
        {
            var certificateKeyPath = Path.Combine(_hostEnvironment.ContentRootPath, certificateConfig.KeyPath);
            var certificate = GetCertificate(certificatePath);

            if (certificate != null)
            {
                certificate = LoadCertificateKey(certificate, certificateKeyPath, certificateConfig.Password);
            }
            else
            {
                throw GetFailedToLoadCertificateKeyException(certificateKeyPath);
            }

            if (certificate != null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return PersistKey(certificate);
                }

                return certificate;
            }

            throw GetFailedToLoadCertificateKeyException(certificateKeyPath);
        }

        private static X509Certificate2 PersistKey(X509Certificate2 fullCertificate)
        {
            // We need to force the key to be persisted.
            // See https://github.com/dotnet/runtime/issues/23749
            var certificateBytes = fullCertificate.Export(X509ContentType.Pkcs12, "");
            return new X509Certificate2(certificateBytes, "", X509KeyStorageFlags.DefaultKeySet);
        }

        private static X509Certificate2 LoadCertificateKey(X509Certificate2 certificate, string keyPath, string password)
        {
            // OIDs for the certificate key types.
            const string RSAOid = "1.2.840.113549.1.1.1";
            const string DSAOid = "1.2.840.10040.4.1";
            const string ECDsaOid = "1.2.840.10045.2.1";

            var keyText = File.ReadAllText(keyPath);
            return certificate.PublicKey.Oid.Value switch
            {
                RSAOid => AttachPemRSAKey(certificate, keyText, password),
                ECDsaOid => AttachPemECDSAKey(certificate, keyText, password),
                DSAOid => AttachPemDSAKey(certificate, keyText, password),
                _ => throw new InvalidOperationException($"Unrecongnized certificate key OID '{certificate.PublicKey.Oid.Value}'.")
            };
        }

        private static X509Certificate2 GetCertificate(string certificatePath)
        {
            if (X509Certificate2.GetCertContentType(certificatePath) == X509ContentType.Cert)
            {
                return new X509Certificate2(certificatePath);
            }

            return null;
        }

        private static X509Certificate2 AttachPemRSAKey(X509Certificate2 certificate, string keyText, string password)
        {
            using var rsa = RSA.Create();
            if (password == null)
            {
                rsa.ImportFromPem(keyText);
            }
            else
            {
                rsa.ImportFromEncryptedPem(keyText, password);
            }

            return certificate.CopyWithPrivateKey(rsa);
        }

        private static X509Certificate2 AttachPemDSAKey(X509Certificate2 certificate, string keyText, string password)
        {
            using var dsa = DSA.Create();
            if (password == null)
            {
                dsa.ImportFromPem(keyText);
            }
            else
            {
                dsa.ImportFromEncryptedPem(keyText, password);
            }

            return certificate.CopyWithPrivateKey(dsa);
        }

        private static X509Certificate2 AttachPemECDSAKey(X509Certificate2 certificate, string keyText, string password)
        {
            using var ecdsa = ECDsa.Create();
            if (password == null)
            {
                ecdsa.ImportFromPem(keyText);
            }
            else
            {
                ecdsa.ImportFromEncryptedPem(keyText, password);
            }

            return certificate.CopyWithPrivateKey(ecdsa);
        }
#elif NETCOREAPP3_1
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif

        private static X509Certificate2 LoadFromCertStore(CertificateConfigData certificateConfig)
        {
            var subject = certificateConfig.Subject;
            var storeName = string.IsNullOrEmpty(certificateConfig.Store) ? StoreName.My.ToString() : certificateConfig.Store;
            var location = certificateConfig.Location;
            var storeLocation = StoreLocation.CurrentUser;
            if (!string.IsNullOrEmpty(location))
            {
                storeLocation = (StoreLocation)Enum.Parse(typeof(StoreLocation), location, ignoreCase: true);
            }

            var allowInvalid = certificateConfig.AllowInvalid ?? false;
            using (var store = new X509Store(storeName, storeLocation))
            {
                X509Certificate2Collection storeCertificates = null;
                X509Certificate2 foundCertificate = null;

                try
                {
                    store.Open(OpenFlags.ReadOnly);
                    storeCertificates = store.Certificates;
                    var foundCertificates = storeCertificates.Find(X509FindType.FindBySubjectName, subject, !allowInvalid);
                    foundCertificate = foundCertificates
                        .OfType<X509Certificate2>()
                        .Where(c => c.HasPrivateKey)
                        .OrderByDescending(certificate => certificate.NotAfter)
                        .FirstOrDefault();

                    if (foundCertificate == null)
                    {
                        throw GetCertificateNotFoundInStoreException(subject, storeName, storeLocation, allowInvalid);
                    }

                    return foundCertificate;
                }
                finally
                {
                    DisposeCertificates(storeCertificates, except: foundCertificate);
                }
            }
        }

        private static X509Certificate2 LoadFromStoreCert(string subject, string storeName, StoreLocation storeLocation, bool allowInvalid)
        {
            using (var store = new X509Store(storeName, storeLocation))
            {
                X509Certificate2Collection storeCertificates = null;
                X509Certificate2 foundCertificate = null;

                try
                {
                    store.Open(OpenFlags.ReadOnly);
                    storeCertificates = store.Certificates;
                    var foundCertificates = storeCertificates.Find(X509FindType.FindBySubjectName, subject, !allowInvalid);
                    foundCertificate = foundCertificates
                        .OfType<X509Certificate2>()
                        .Where(c => c.HasPrivateKey)
                        .OrderByDescending(certificate => certificate.NotAfter)
                        .FirstOrDefault();

                    if (foundCertificate == null)
                    {
                        throw GetCertificateNotFoundInStoreException(subject, storeName, storeLocation, allowInvalid);
                    }

                    return foundCertificate;
                }
                finally
                {
                    DisposeCertificates(storeCertificates, except: foundCertificate);
                }
            }
        }

        private static void DisposeCertificates(X509Certificate2Collection certificates, X509Certificate2 except)
        {
            if (certificates != null)
            {
                foreach (var certificate in certificates)
                {
                    if (!certificate.Equals(except))
                    {
                        certificate.Dispose();
                    }
                }
            }
        }

        private static InvalidOperationException GetFailedToLoadCertificateKeyException(string certificateKeyPath)
        {
            return new InvalidOperationException($"Failed to load a certificate key from the specified path '{certificateKeyPath}'.");
        }

        private static InvalidOperationException GetCertificateNotFoundInStoreException(string subject, string storeName, StoreLocation storeLocation, bool allowInvalid)
        {
            return new InvalidOperationException($"Certificate was not found in the store. Parameters: Subject={subject}, StoreLocation={storeLocation}, StoreName={storeName}, AllowInvalid={allowInvalid}");
        }

        private class CertificateConfigData
        {
            public string Path { get; set; }

            public string KeyPath { get; set; }

            public string Password { get; set; }

            public string Subject { get; set; }

            public string Store { get; set; }

            public string Location { get; set; }

            public bool? AllowInvalid { get; set; }

            internal bool IsFileCert => !string.IsNullOrEmpty(Path);

            internal bool IsStoreCert => !string.IsNullOrEmpty(Subject);
        }
    }
}
