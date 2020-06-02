// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public static class AffinityTestHelper
    {
        public static Mock<ILogger<T>> GetLogger<T>()
        {
            return new Mock<ILogger<T>>();
        }

        public static Mock<IDataProtector> GetDataProtector()
        {
            var protector = new Mock<IDataProtector>();
            protector.Setup(p => p.CreateProtector(It.IsAny<string>())).Returns(protector.Object);
            protector.Setup(p => p.Protect(It.IsAny<byte[]>())).Returns((byte[] b) => b);
            protector.Setup(p => p.Unprotect(It.IsAny<byte[]>())).Returns((byte[] b) => b);
            return protector;
        }

        public static string ToUTF8BytesInBase64(this string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }
    }
}
