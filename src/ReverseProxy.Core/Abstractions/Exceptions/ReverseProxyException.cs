// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// Represents unexpected proxy errors.
    /// </summary>
    public sealed class ReverseProxyException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReverseProxyException"/> class.
        /// </summary>
        public ReverseProxyException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReverseProxyException"/> class.
        /// </summary>
        public ReverseProxyException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReverseProxyException"/> class.
        /// </summary>
        public ReverseProxyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
