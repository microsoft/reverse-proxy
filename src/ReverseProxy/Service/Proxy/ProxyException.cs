// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Used to report errors that occur while proxying a request to the destination.
    /// </summary>
    public class ProxyException : Exception
    {
        /// <summary>
        /// Creates a new ProxyException.
        /// </summary>
        public ProxyException(ProxyErrorCode errorCode, Exception innerException) : base(GetMessage(errorCode), innerException)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// The specified ProxyErrorCode.
        /// </summary>
        public ProxyErrorCode ErrorCode { get; }

        internal static string GetMessage(ProxyErrorCode errorCode)
        {
            return errorCode switch
            {
                ProxyErrorCode.None => throw new NotSupportedException("A more specific error code must be used"),
                ProxyErrorCode.Request => "An error was encountered when sending the request to the destination.",
                ProxyErrorCode.RequestCanceled => "The request was canceled while sending it to the destination.",
                ProxyErrorCode.RequestBodyCanceled => "Copying the request body was canceled.",
                ProxyErrorCode.RequestBodyClient => "The client reported an error when copying the request body.",
                ProxyErrorCode.RequestBodyDestination => "The destination reported an error when copying the request body.",
                ProxyErrorCode.ResponseBodyCanceled => "Copying the response body was canceled.",
                ProxyErrorCode.ResponseBodyClient => "The client reported an error when copying the response body.",
                ProxyErrorCode.ResponseBodyDestination => "The destination reported an error when copying the response body.",
                ProxyErrorCode.UpgradeRequestCanceled => "Copying the upgraded request body was canceled.",
                ProxyErrorCode.UpgradeRequestClient => "The client reported an error when copying the upgraded request body.",
                ProxyErrorCode.UpgradeRequestDestination => "The destination reported an error when copying the upgraded request body.",
                ProxyErrorCode.UpgradeResponseCanceled => "Copying the upgraded response body was canceled.",
                ProxyErrorCode.UpgradeResponseClient => "The client reported an error when copying the upgraded response body.",
                ProxyErrorCode.UpgradeResponseDestination => "The destination reported an error when copying the upgraded response body.",
                _ => throw new NotImplementedException(errorCode.ToString()),
            };
        }
    }
}
