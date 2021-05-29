// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// An implementation of the Forwarded header as defined in https://tools.ietf.org/html/rfc7239.
    /// </summary>
    public class RequestHeaderForwardedTransform : RequestTransform
    {
        private static readonly string ForwardedHeaderName = "Forwarded";
        // obfnode = "_" 1*( ALPHA / DIGIT / "." / "_" / "-")
        private static readonly string ObfChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-";

        private readonly IRandomFactory _randomFactory;

        public RequestHeaderForwardedTransform(IRandomFactory randomFactory, NodeFormat forFormat, NodeFormat byFormat, bool host, bool proto, bool append)
        {
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
            ForFormat = forFormat;
            ByFormat = byFormat;
            HostEnabled = host;
            ProtoEnabled = proto;
            Append = append;
        }

        internal NodeFormat ForFormat { get; }

        internal NodeFormat ByFormat { get; }

        internal bool HostEnabled { get; }

        internal bool ProtoEnabled { get; }

        internal bool Append { get; }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var httpContext = context.HttpContext;

            var builder = new ValueStringBuilder();
            AppendProto(httpContext, ref builder);
            AppendHost(httpContext, ref builder);
            AppendFor(httpContext, ref builder);
            AppendBy(httpContext, ref builder);
            var value = builder.ToString();

            var existingValues = TakeHeader(context, ForwardedHeaderName);
            if (Append)
            {
                var values = StringValues.Concat(existingValues, value);
                AddHeader(context, ForwardedHeaderName, values);
            }
            else
            {
                AddHeader(context, ForwardedHeaderName, value);
            }

            return default;
        }

        private void AppendProto(HttpContext context, ref ValueStringBuilder builder)
        {
            if (ProtoEnabled)
            {
                // Always first doesn't need to check for ';'
                builder.Append("proto=");
                builder.Append(context.Request.Scheme);
            }
        }

        private void AppendHost(HttpContext context, ref ValueStringBuilder builder)
        {
            if (HostEnabled)
            {
                if (builder.Length > 0)
                {
                    builder.Append(';');
                }
                // Quoted because of the ':' when there's a port.
                builder.Append("host=\"");
                builder.Append(context.Request.Host.ToUriComponent());
                builder.Append('"');
            }
        }

        private void AppendFor(HttpContext context, ref ValueStringBuilder builder)
        {
            if (ForFormat > NodeFormat.None)
            {
                if (builder.Length > 0)
                {
                    builder.Append(';');
                }
                builder.Append("for=");
                AppendNode(context.Connection.RemoteIpAddress, context.Connection.RemotePort, ForFormat, ref builder);
            }
        }

        private void AppendBy(HttpContext context, ref ValueStringBuilder builder)
        {
            if (ByFormat > NodeFormat.None)
            {
                if (builder.Length > 0)
                {
                    builder.Append(';');
                }
                builder.Append("by=");
                AppendNode(context.Connection.LocalIpAddress, context.Connection.LocalPort, ByFormat, ref builder);
            }
        }

        // https://tools.ietf.org/html/rfc7239#section-6
        private void AppendNode(IPAddress? ipAddress, int port, NodeFormat format, ref ValueStringBuilder builder)
        {
            // "It is important to note that an IPv6 address and any nodename with
            // node-port specified MUST be quoted, since ":" is not an allowed
            // character in "token"."
            var addPort = port != 0 && (format == NodeFormat.IpAndPort || format == NodeFormat.UnknownAndPort || format == NodeFormat.RandomAndPort);
            var addRandomPort = (format == NodeFormat.IpAndRandomPort || format == NodeFormat.UnknownAndRandomPort || format == NodeFormat.RandomAndRandomPort);
            var ipv6 = (format == NodeFormat.Ip || format == NodeFormat.IpAndPort || format == NodeFormat.IpAndRandomPort)
                && ipAddress != null && ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            var quote = addPort || addRandomPort || ipv6;

            if (quote)
            {
                builder.Append('"');
            }

            switch (format)
            {
                case NodeFormat.Ip:
                case NodeFormat.IpAndPort:
                case NodeFormat.IpAndRandomPort:
                    if (ipAddress != null)
                    {
                        if (ipv6)
                        {
                            builder.Append('[');
                        }
                        builder.Append(ipAddress.ToString());
                        if (ipv6)
                        {
                            builder.Append(']');
                        }
                        break;
                    }
                    // This primarily happens in test environments that don't use real connections.
                    goto case NodeFormat.Unknown;
                case NodeFormat.Unknown:
                case NodeFormat.UnknownAndPort:
                case NodeFormat.UnknownAndRandomPort:
                    builder.Append("unknown");
                    break;
                case NodeFormat.Random:
                case NodeFormat.RandomAndPort:
                case NodeFormat.RandomAndRandomPort:
                    AppendRandom(ref builder);
                    break;
                default:
                    throw new NotImplementedException(format.ToString());
            }

            if (addPort)
            {
                builder.Append(':');
                builder.Append(port);
            }
            else if (addRandomPort)
            {
                builder.Append(':');
                AppendRandom(ref builder);
            }

            if (quote)
            {
                builder.Append('"');
            }
        }

        // https://tools.ietf.org/html/rfc7239#section-6.3
        private void AppendRandom(ref ValueStringBuilder builder)
        {
            var random = _randomFactory.CreateRandomInstance();
            builder.Append('_');
            // This length is arbitrary.
            for (var i = 0; i < 9; i++)
            {
                builder.Append(ObfChars[random.Next(ObfChars.Length)]);
            }
        }
    }
}
