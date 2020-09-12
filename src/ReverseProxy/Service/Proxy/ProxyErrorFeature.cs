// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    internal class ProxyErrorFeature : IProxyErrorFeature
    {
        internal ProxyErrorFeature(ProxyErrorCode errorCode, Exception ex)
        {
            ErrorCode = errorCode;
            Error = ex;
        }

        /// <summary>
        /// The specified ProxyErrorCode.
        /// </summary>
        public ProxyErrorCode ErrorCode { get; }

        /// <summary>
        /// The error, if any.
        /// </summary>
        public Exception Error { get; }
    }
}
