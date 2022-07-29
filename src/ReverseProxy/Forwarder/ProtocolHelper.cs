// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

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

    /// <summary>
    /// Checks whether the provided content type header value represents a gRPC request.
    /// </summary>
    public static bool IsGrpcContentType(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var mediaType)
        && mediaType.MatchesMediaType(GrpcContentType);
}
