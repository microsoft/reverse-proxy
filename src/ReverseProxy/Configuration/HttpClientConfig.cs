// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security.Authentication;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Options used for communicating with the destination servers.
/// </summary>
public sealed record HttpClientConfig
{
    /// <summary>
    /// An empty options instance.
    /// </summary>
    public static readonly HttpClientConfig Empty = new();

    /// <summary>
    /// What TLS protocols to use.
    /// </summary>
    public SslProtocols? SslProtocols { get; init; }

    /// <summary>
    /// Indicates if destination server https certificate errors should be ignored.
    /// This should only be done when using self-signed certificates.
    /// </summary>
    public bool? DangerousAcceptAnyServerCertificate { get; init; }

    /// <summary>
    /// Limits the number of connections used when communicating with the destination server.
    /// </summary>
    public int? MaxConnectionsPerServer { get; init; }

    /// <summary>
    /// Optional web proxy used when communicating with the destination server. 
    /// </summary>
    public WebProxyConfig? WebProxy { get; init; }

    /// <summary>
    /// Gets or sets a value that indicates whether additional HTTP/2 connections can
    /// be established to the same server when the maximum number of concurrent streams
    /// is reached on all existing connections.
    /// </summary>
    public bool? EnableMultipleHttp2Connections { get; init; }

    /// <summary>
    /// Enables non-ASCII header encoding for outgoing requests.
    /// </summary>
    public string? RequestHeaderEncoding { get; init; }

    public bool Equals(HttpClientConfig? other)
    {
        if (other is null)
        {
            return false;
        }

        return SslProtocols == other.SslProtocols
               && DangerousAcceptAnyServerCertificate == other.DangerousAcceptAnyServerCertificate
               && MaxConnectionsPerServer == other.MaxConnectionsPerServer
               && EnableMultipleHttp2Connections == other.EnableMultipleHttp2Connections
               // Comparing by reference is fine here since Encoding.GetEncoding returns the same instance for each encoding.
               && RequestHeaderEncoding == other.RequestHeaderEncoding
               && WebProxy == other.WebProxy;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(SslProtocols,
            DangerousAcceptAnyServerCertificate,
            MaxConnectionsPerServer,
            EnableMultipleHttp2Connections,
            RequestHeaderEncoding,
            WebProxy);
    }
}
