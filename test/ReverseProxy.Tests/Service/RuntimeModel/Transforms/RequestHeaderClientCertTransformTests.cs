// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class RequestHeaderClientCertTransformTests
    {
        [Fact]
        public void NoCert_NoOp()
        {
            var httpContext = new DefaultHttpContext();
            var transform = new RequestHeaderClientCertTransform();
            var result = transform.Apply(httpContext, StringValues.Empty);
            Assert.Equal(StringValues.Empty, result);
        }

        [Fact]
        public void Cert_Encoded()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.ClientCertificate = Certificates.SelfSignedValidWithClientEku;
            var transform = new RequestHeaderClientCertTransform();
            var result = transform.Apply(httpContext, StringValues.Empty);
            var expected = Convert.ToBase64String(Certificates.SelfSignedValidWithClientEku.RawData);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ExistingHeader_NoCert_ReturnsEmpty()
        {
            var httpContext = new DefaultHttpContext();
            var transform = new RequestHeaderClientCertTransform();
            var result = transform.Apply(httpContext, "OtherValue");
            Assert.Equal(StringValues.Empty, result);
        }

        [Fact]
        public void ExistingHeader_Replaced()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.ClientCertificate = Certificates.SelfSignedValidWithClientEku;
            var transform = new RequestHeaderClientCertTransform();
            var result = transform.Apply(httpContext, "OtherValue");
            var expected = Convert.ToBase64String(Certificates.SelfSignedValidWithClientEku.RawData);
            Assert.Equal(expected, result);
        }

        private static class Certificates
        {
            public static X509Certificate2 SelfSignedValidWithClientEku { get; private set; } =
                new X509Certificate2(GetFullyQualifiedFilePath("validSelfSignedClientEkuCertificate.cer"));

            private static string GetFullyQualifiedFilePath(string filename)
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, filename);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException(filePath);
                }
                return filePath;
            }
        }
    }
}
