// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Forwarder
{
    internal static class ProtocolHelper
    {
#if NET
        internal static readonly Version Http2Version = HttpVersion.Version20;
        internal static readonly Version Http11Version = HttpVersion.Version11;
#elif NETCOREAPP3_1
        internal static readonly Version Http2Version = new Version(2, 0);
        internal static readonly Version Http11Version = new Version(1, 1);
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif

        internal const string GrpcContentType = "application/grpc";

        public static bool IsHttp2(string protocol)
        {
#if NET
            return Microsoft.AspNetCore.Http.HttpProtocol.IsHttp2(protocol);
#elif NETCOREAPP3_1
            return StringComparer.OrdinalIgnoreCase.Equals("HTTP/2", protocol);
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
        }

        public static bool IsHttp2OrGreater(string protocol)
        {
#if NET
            return Microsoft.AspNetCore.Http.HttpProtocol.IsHttp2(protocol) || Microsoft.AspNetCore.Http.HttpProtocol.IsHttp3(protocol);
#elif NETCOREAPP3_1
            return StringComparer.OrdinalIgnoreCase.Equals("HTTP/2", protocol) || StringComparer.OrdinalIgnoreCase.Equals("HTTP/3", protocol);
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
        }

        public static string GetHttpProtocol(Version version)
        {
#if NET
            return HttpProtocol.GetHttpProtocol(version);
#else
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            return version switch
            {
                { Major: 3, Minor: 0 } => "HTTP/3",
                { Major: 2, Minor: 0 } => "HTTP/2",
                { Major: 1, Minor: 1 } => "HTTP/1.1",
                { Major: 1, Minor: 0 } => "HTTP/1.0",
                { Major: 0, Minor: 9 } => "HTTP/0.9",
                _ => throw new ArgumentOutOfRangeException(nameof(version), "Version doesn't map to a known HTTP protocol.")
            };
#endif
        }
#if NET
        public static string GetVersionPolicy(HttpVersionPolicy policy)
        {
            return policy switch
            {
                HttpVersionPolicy.RequestVersionOrLower => "RequestVersionOrLower",
                HttpVersionPolicy.RequestVersionOrHigher => "RequestVersionOrHigher",
                HttpVersionPolicy.RequestVersionExact => "RequestVersionExact",
                _ => throw new NotImplementedException(policy.ToString()),
            };
        }
#endif
        // NOTE: When https://github.com/dotnet/aspnetcore/issues/21265 is addressed,
        // this can be replaced with `MediaTypeHeaderValue.IsSubsetOf(...)`.
        /// <summary>
        /// Checks whether the provided content type header value represents a gRPC request.
        /// Takes inspiration from
        /// <see href="https://github.com/grpc/grpc-dotnet/blob/3ce9b104524a4929f5014c13cd99ba9a1c2431d4/src/Shared/CommonGrpcProtocolHelpers.cs#L26"/>.
        /// </summary>
        public static bool IsGrpcContentType(string contentType)
        {
            if (contentType == null)
            {
                return false;
            }

            if (!contentType.StartsWith(GrpcContentType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (contentType.Length == GrpcContentType.Length)
            {
                // Exact match
                return true;
            }

            // Support variations on the content-type (e.g. +proto, +json)
            var nextChar = contentType[GrpcContentType.Length];
            if (nextChar == ';')
            {
                return true;
            }
            if (nextChar == '+')
            {
                // Accept any message format. Marshaller could be set to support third-party formats
                return true;
            }

            return false;
        }
    }
}
