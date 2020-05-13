// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    internal static class HttpUtilities
    {
        /// <summary>
        /// Converts the given HTTP method (usually obtained from <see cref="HttpRequest.Method"/>)
        /// into the corresponding <see cref="HttpMethod"/> static instance.
        /// </summary>
        public static HttpMethod GetHttpMethod(string method) => method switch
        {
            string mth when HttpMethods.IsGet(mth) => HttpMethod.Get,
            string mth when HttpMethods.IsPost(mth) => HttpMethod.Post,
            string mth when HttpMethods.IsPut(mth) => HttpMethod.Put,
            string mth when HttpMethods.IsDelete(mth) => HttpMethod.Delete,
            string mth when HttpMethods.IsOptions(mth) => HttpMethod.Options,
            string mth when HttpMethods.IsHead(mth) => HttpMethod.Head,
            string mth when HttpMethods.IsPatch(mth) => HttpMethod.Patch,
            string mth when HttpMethods.IsTrace(mth) => HttpMethod.Trace,
            // NOTE: Proxying "CONNECT" is not supported (by design!)
            _ => throw new InvalidOperationException($"Unsupported request method '{method}'.")
        };
    }
}
