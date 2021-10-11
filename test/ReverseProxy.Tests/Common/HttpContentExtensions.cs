// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Tests.Common
{
    internal static class HttpContentExtensions
    {
        public static Task CopyToWithCancellationAsync(this HttpContent httpContent, Stream stream)
        {
            // StreamCopyHttpContent assumes that the cancellation token passed to it can always be canceled (.NET 5+).
            // This is the case for real callers, so we insert a dummy CTS in tests to allow us to keep the debug assertion.
#if NET
            return httpContent.CopyToAsync(stream, new CancellationTokenSource().Token);
#else
            return httpContent.CopyToAsync(stream);
#endif
        }
    }
}
