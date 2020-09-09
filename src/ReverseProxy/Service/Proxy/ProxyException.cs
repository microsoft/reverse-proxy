// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    public class ProxyException : Exception
    {
        public ProxyException(string message) : base(message)
        {
        }

        public ProxyException(ProxyErrorCode errorCode, Exception innerException) : base(GetMessage(errorCode), innerException)
        {
            ErrorCode = errorCode;
        }

        public ProxyErrorCode ErrorCode { get; }

        private static string GetMessage(ProxyErrorCode errorCode)
        {
            return errorCode switch
            {
                ProxyErrorCode.None => throw new NotSupportedException("A more specific error code must be used"),
                ProxyErrorCode.Request => "An error was encountered when sending the request.",
                ProxyErrorCode.RequestBodyClient => "The client reported an error when copying the request body.",
                ProxyErrorCode.ResponseBodyCanceled => "Copying the response body was canceled.",
                ProxyErrorCode.ResponseBodyClient => "The client reported an error when copying the response body.",
                ProxyErrorCode.ResponseBodyDestination => "The destination reported an error when copying the response body.",
                _ => "An error occurred when proxying the request: " + errorCode,
            };
        }
    }
}
