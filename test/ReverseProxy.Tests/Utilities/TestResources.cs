// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Xunit;

namespace Microsoft.ReverseProxy.Utilities.Tests
{
    public static class TestResources
    {
        private const int MutexTimeout = 120 * 1000;
        private static readonly Mutex importPfxMutex = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
            new Mutex(initiallyOwned: false, "Global\\Microsoft.ReverseProxy.Tests.Certificates.LoadPfxCertificate") :
            null;

        public static X509Certificate2 GetTestCertificate(string certName = "testCert.pfx")
        {
            // On Windows, applications should not import PFX files in parallel to avoid a known system-level
            // race condition bug in native code which can cause crashes/corruption of the certificate state.
            if (importPfxMutex != null)
            {
                Assert.True(importPfxMutex.WaitOne(MutexTimeout), "Cannot acquire the global certificate mutex.");
            }

            try
            {
                return new X509Certificate2(GetCertPath(certName), "testPassword");
            }
            finally
            {
                importPfxMutex?.ReleaseMutex();
            }
        }

        public static string GetCertPath(string fileName)
        {
            if (fileName == null)
            {
                return null;
            }

            var basePath = Path.Combine(Directory.GetCurrentDirectory(), @"..\..\..\..\..\test", "TestCertificates");
            return Path.Combine(basePath, fileName);
        }
    }
}
