// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Forwarder;

internal static class ProtocolHelper
{
    internal static readonly Version Http2Version = HttpVersion.Version20;
    internal static readonly Version Http11Version = HttpVersion.Version11;

    internal const string GrpcContentType = "application/grpc";

    public static bool IsHttp2OrGreater(string protocol) => HttpProtocol.IsHttp2(protocol) || HttpProtocol.IsHttp3(protocol);

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

    // NOTE: When https://github.com/dotnet/aspnetcore/issues/21265 is addressed,
    // this can be replaced with `MediaTypeHeaderValue.IsSubsetOf(...)`.
    /// <summary>
    /// Checks whether the provided content type header value represents a gRPC request.
    /// Takes inspiration from
    /// <see href="https://github.com/grpc/grpc-dotnet/blob/3ce9b104524a4929f5014c13cd99ba9a1c2431d4/src/Shared/CommonGrpcProtocolHelpers.cs#L26"/>.
    /// </summary>
    public static bool IsGrpcContentType(string? contentType)
    {
        if (contentType is null)
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
