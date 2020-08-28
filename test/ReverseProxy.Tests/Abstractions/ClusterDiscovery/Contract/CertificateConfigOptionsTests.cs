// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class CertificateConfigOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new CertificateConfigOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var options = new CertificateConfigOptions
            {
                AllowInvalid = true,
                KeyPath = "testKeyPath",
                Location = "testLocation",
                Password = "testPassword",
                Path = "testPath",
                Store = "testStore",
                Subject = "testSubject"
            };

            // Act
            var clone = options.DeepClone();

            // Assert
            Assert.NotSame(options, clone);
            Assert.Equal(options.AllowInvalid, clone.AllowInvalid);
            Assert.Equal(options.KeyPath, clone.KeyPath);
            Assert.Equal(options.Location, clone.Location);
            Assert.Equal(options.Password, clone.Password);
            Assert.Equal(options.Path, clone.Path);
            Assert.Equal(options.Store, clone.Store);
            Assert.Equal(options.Subject, clone.Subject);
        }

        [Theory]
        [InlineData(null, null, false, false)]
        [InlineData("", "", false, false)]
        [InlineData("somePath", null, true, false)]
        [InlineData(null, "someSubject", false, true)]
        [InlineData("somePath", "someSubject", true, true)]
        public void IsStoreCertOrIsFileCert_Success(string path, string subject, bool isFileCert, bool isStoreCert)
        {
            var options = new CertificateConfigOptions { Path = path, Subject = subject };

            Assert.Equal(isFileCert, options.IsFileCert);
            Assert.Equal(isStoreCert, options.IsStoreCert);
        }
    }
}
