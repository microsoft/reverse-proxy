// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Utilities.Tests;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Configuration
{
    public class CertificateConfigLoaderTests
    {
        [Fact]
        public void LoadCertificate_PfxPathAndPasswordSpecified_Success()
        {
            var loader = new CertificateConfigLoader(GetHostEnvironment());
            var options = new CertificateConfigData
            {
                Path = TestResources.GetCertPath("aspnetdevcert.pfx"),
                Password = "testPassword"
            };
            var certificate = loader.LoadCertificate(options.AsConfigurationSection());

            Assert.NotNull(certificate);
            Assert.Equal("7E2467E85A9FA8824F6A37469334AD1C", certificate.SerialNumber);
        }

        [Fact]
        public void LoadCertificate_PfxPasswordIsNotCorrect_Throws()
        {
            var loader = new CertificateConfigLoader(GetHostEnvironment());
            var options = new CertificateConfigData
            {
                Path = TestResources.GetCertPath("aspnetdevcert.pfx"),
                Password = "12341234"
            };
            Assert.ThrowsAny<CryptographicException>(() => loader.LoadCertificate(options.AsConfigurationSection()));
        }

        [Fact]
        public void LoadCertificate_PfxFileNotFound_Throws()
        {
            var loader = new CertificateConfigLoader(GetHostEnvironment());
            var options = new CertificateConfigData
            {
                Path = TestResources.GetCertPath("missingfile.pfx"),
                Password = "testPassword"
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Assert.ThrowsAny<FileNotFoundException>(() => loader.LoadCertificate(options.AsConfigurationSection()));
            }
            else
            {
                Assert.ThrowsAny<CryptographicException>(() => loader.LoadCertificate(options.AsConfigurationSection()));
            }
        }

#if NET5_0
        [Fact]
        public void LoadCertificate_PemPathAndKeySpecifiedButPasswordIsMissing_Throws()
        {
            var loader = new CertificateConfigLoader(GetHostEnvironment());
            var options = new CertificateConfigData
            {
                Path = TestResources.GetCertPath("https-aspnet.crt"),
                KeyPath = TestResources.GetCertPath("https-aspnet.key")
            };
            Assert.Throws<ArgumentException>(() => loader.LoadCertificate(options.AsConfigurationSection()));
        }

        [Fact]
        public void LoadCertificate_PemKeyDoesntMatchTheCertificateKey_Throws()
        {
            var loader = new CertificateConfigLoader(GetHostEnvironment());
            var options = new CertificateConfigData
            {
                Path = TestResources.GetCertPath("https-aspnet.crt"),
                KeyPath = TestResources.GetCertPath("https-ecdsa.key")
            };
            Assert.Throws<ArgumentException>(() => loader.LoadCertificate(options.AsConfigurationSection()));
        }

        [Fact]
        public void LoadCertificate_PemPasswordIsIncorrect_Throws()
        {
            var loader = new CertificateConfigLoader(GetHostEnvironment());
            var options = new CertificateConfigData
            {
                Path = TestResources.GetCertPath("https-aspnet.crt"),
                KeyPath = TestResources.GetCertPath("https-aspnet.key"),
                Password = "abcde"
            };
            Assert.ThrowsAny<CryptographicException>(() => loader.LoadCertificate(options.AsConfigurationSection()));
        }

        [Theory]
        [InlineData("https-rsa.pem", "https-rsa.key", null, "4BE331DBFC1330C4")]
        [InlineData("https-rsa.pem", "https-rsa-protected.key", "aspnetcore", "4BE331DBFC1330C4")]
        [InlineData("https-rsa.crt", "https-rsa.key", null, "4BE331DBFC1330C4")]
        [InlineData("https-rsa.crt", "https-rsa-protected.key", "aspnetcore", "4BE331DBFC1330C4")]
        [InlineData("https-ecdsa.pem", "https-ecdsa.key", null, "50787B154489A128129A12399678A8DBF47EFA05")]
        [InlineData("https-ecdsa.pem", "https-ecdsa-protected.key", "aspnetcore", "50787B154489A128129A12399678A8DBF47EFA05")]
        [InlineData("https-ecdsa.crt", "https-ecdsa.key", null, "50787B154489A128129A12399678A8DBF47EFA05")]
        [InlineData("https-ecdsa.crt", "https-ecdsa-protected.key", "aspnetcore", "50787B154489A128129A12399678A8DBF47EFA05")]
        [InlineData("https-dsa.pem", "https-dsa.key", null, "15140603DD061C2EF870D2BF84DCD00E2ED72456")]
        [InlineData("https-dsa.pem", "https-dsa-protected.key", "test", "15140603DD061C2EF870D2BF84DCD00E2ED72456")]
        [InlineData("https-dsa.crt", "https-dsa.key", null, "15140603DD061C2EF870D2BF84DCD00E2ED72456")]
        [InlineData("https-dsa.crt", "https-dsa-protected.key", "test", "15140603DD061C2EF870D2BF84DCD00E2ED72456")]
        public void LoadCertificate_PemLoadCertificate_Success(string certificateFile, string certificateKey, string password, string expectedSN)
        {
            var loader = new CertificateConfigLoader(GetHostEnvironment());
            var options = new CertificateConfigData
            {
                Path = TestResources.GetCertPath(certificateFile),
                KeyPath = TestResources.GetCertPath(certificateKey),
                Password = password
            };

            var certificate = loader.LoadCertificate(options.AsConfigurationSection());
            Assert.Equal(expectedSN, certificate.SerialNumber);
        }
#endif
        private static IWebHostEnvironment GetHostEnvironment()
        {
            var result = new Mock<IWebHostEnvironment>();
            result.SetupGet(r => r.ContentRootPath).Returns(string.Empty);
            return result.Object;
        }

        private class CertificateConfigData
        {
            public string Password { get; set; }
            public string KeyPath { get; set; }
            public string Path { get; set; }

            public IConfigurationSection AsConfigurationSection() => new ConfigurationSection(new Dictionary<string, string>()
            {
                { nameof(Path), Path },
                { nameof(KeyPath), KeyPath },
                { nameof(Password), Password }
            });
        }

        private class ConfigurationSection : IConfigurationSection
        {
            private readonly IDictionary<string, string> _items;
            public ConfigurationSection(Dictionary<string, string> items)
            {
                _items = items;
                Value = "";
            }

            public string this[string key] { get => _items.TryGetValue(key, out var item) ? item : default; set => _items[key] = value; }

            public string Key => null;

            public string Path => null;

            public string Value { get; set; }

            public IEnumerable<IConfigurationSection> GetChildren()
            {
                yield break;
            }

            public IChangeToken GetReloadToken()
            {
                return null;
            }

            public IConfigurationSection GetSection(string key)
            {
                return null;
            }
        }
    }
}
