// <copyright file="HttpUtilities.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace IslandGateway.Core.Service.Proxy
{
    internal static class HttpUtilities
    {
        /// <summary>
        /// Converts the given HTTP method (usually obtained from <see cref="HttpRequest.Method"/>)
        /// into the corresponding <see cref="HttpMethod"/> static instance.
        /// </summary>
        public static HttpMethod GetHttpMethod(string method)
        {
            // NOTE: ASP .NET Core always produces HttpRequest.Method with the exact strings below.
            // See: https://github.com/dotnet/aspnetcore/blob/master/src/Servers/Kestrel/Core/src/Internal/Infrastructure/HttpUtilities.Generated.cs
            switch (method)
            {
                case "GET":
                    return HttpMethod.Get;
                case "POST":
                    return HttpMethod.Post;
                case "PUT":
                    return HttpMethod.Put;
                case "DELETE":
                    return HttpMethod.Delete;
                case "OPTIONS":
                    return HttpMethod.Options;
                case "HEAD":
                    return HttpMethod.Head;
                case "PATCH":
                    return HttpMethod.Patch;
                case "TRACE":
                    return HttpMethod.Trace;

                // NOTE: Proxying "CONNECT" is not supported (by design!)
                ////case "CONNECT":
                ////    return new HttpMethod("CONNECT");
            }

            throw new InvalidOperationException($"Unsupported request method '{method}'.");
        }

        /// <summary>
        /// Checks whether the given protocol (usually obtained from <see cref="HttpRequest.Protocol"/>)
        /// is HTTP 2.0.
        /// </summary>
        public static bool IsHttp2(string protocol)
        {
            return string.Equals("HTTP/2", protocol, StringComparison.OrdinalIgnoreCase);
        }
    }
}
