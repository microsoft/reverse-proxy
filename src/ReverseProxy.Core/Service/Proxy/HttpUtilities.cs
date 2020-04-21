// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace Microsoft.ReverseProxy.Core.Service.Proxy
{
    internal static class HttpUtilities
    {
        /// <summary>
        /// Converts the given HTTP method (usually obtained from <see cref="HttpRequest.Method"/>)
        /// into the corresponding <see cref="HttpMethod"/> static instance.
        /// </summary>
        public static HttpMethod GetHttpMethod(string method)
        {
            if (HttpMethods.IsGet(method))
            {
                return HttpMethod.Get;
            }

            if (HttpMethods.IsPost(method))
            {
                return HttpMethod.Post;
            }

            if (HttpMethods.IsPut(method))
            {
                return HttpMethod.Put;
            }

            if (HttpMethods.IsDelete(method))
            {
                return HttpMethod.Delete;
            }

            if (HttpMethods.IsOptions(method))
            {
                return HttpMethod.Options;
            }

            if (HttpMethods.IsHead(method))
            {
                return HttpMethod.Head;
            }

            if (HttpMethods.IsPatch(method))
            {
                return HttpMethod.Patch;
            }

            if (HttpMethods.IsTrace(method))
            {
                return HttpMethod.Trace;
            }

            // NOTE: Proxying "CONNECT" is not supported (by design!)
            //if (HttpMethods.IsConnect(method))
            //{
            //    return new HttpMethod("CONNECT");
            //}

            throw new InvalidOperationException($"Unsupported request method '{method}'.");
        }
    }
}
