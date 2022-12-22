// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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
        contentType is not null
        && contentType.StartsWith(GrpcContentType, StringComparison.OrdinalIgnoreCase)
        && MediaTypeHeaderValue.TryParse(contentType, out var mediaType)
        && mediaType.MatchesMediaType(GrpcContentType);

    /// <summary>
    /// Creates a security key for sending in the Sec-WebSocket-Key header.
    /// </summary>
    internal static string CreateSecWebSocketKey()
    {
        Span<byte> bytes = stackalloc byte[16];
        // Base64-encode a new Guid's bytes to get the security key
        var success = Guid.NewGuid().TryWriteBytes(bytes);
        Debug.Assert(success);
        var secKey = Convert.ToBase64String(bytes);
        return secKey;
    }

    /// <summary>
    /// Creates the Accept response to a given security key for sending in or verifying the Sec-WebSocket-Accept header value.
    /// </summary>
    internal static string CreateSecWebSocketAccept(string? key)
    {
        // GUID appended by the server as part of the security key response.  Defined in the RFC.
        var wsServerGuidBytes = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"u8;
        Span<byte> bytes = stackalloc byte[24 /* Base64 guid length */ + wsServerGuidBytes.Length];

        // Get the corresponding ASCII bytes for seckey+wsServerGuidBytes
        var encodedSecKeyLength = Encoding.ASCII.GetBytes(key, bytes);
        wsServerGuidBytes.CopyTo(bytes.Slice(encodedSecKeyLength));

        // Hash the seckey+wsServerGuidBytes bytes
        SHA1.TryHashData(bytes, bytes, out var bytesWritten);
        Debug.Assert(bytesWritten == 20 /* SHA1 hash length */);
        var accept = Convert.ToBase64String(bytes[..bytesWritten]);

        // Return the security key + accept value
        return accept;
    }
}
