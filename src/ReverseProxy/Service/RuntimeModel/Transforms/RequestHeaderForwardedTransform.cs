// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// An implementation of the Forwarded header as defined in https://tools.ietf.org/html/rfc7239.
    /// </summary>
    internal class RequestHeaderForwardedTransform : RequestHeaderTransform
    {
        // obfnode = "_" 1*( ALPHA / DIGIT / "." / "_" / "-")
        private static readonly string ObfChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-";

        private readonly IRandomFactory _randomFactory;
        private readonly NodeFormat _forFormat;
        private readonly NodeFormat _byFormat;
        private readonly bool _hostEnabled;
        private readonly bool _protoEnabled;
        private readonly bool _append;

        public RequestHeaderForwardedTransform(IRandomFactory randomFactory, NodeFormat forFormat, NodeFormat byFormat, bool host, bool proto, bool append)
        {
            _randomFactory = randomFactory;
            _forFormat = forFormat;
            _byFormat = byFormat;
            _hostEnabled = host;
            _protoEnabled = proto;
            _append = append;
        }

        public override StringValues Apply(HttpContext context, StringValues values)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var builder = new StringBuilder();
            AppendProto(context, builder);
            AppendHost(context, builder);
            AppendFor(context, builder);
            AppendBy(context, builder);
            var value = builder.ToString();

            if (_append)
            {
                return StringValues.Concat(values, value);
            }

            // Set
            return value;
        }

        private void AppendProto(HttpContext context, StringBuilder builder)
        {
            if (_protoEnabled)
            {
                // Always first doesn't need to check for ';'
                builder.Append("proto=");
                builder.Append(context.Request.Scheme);
            }
        }

        private void AppendHost(HttpContext context, StringBuilder builder)
        {
            if (_hostEnabled)
            {
                if (builder.Length > 0)
                {
                    builder.Append(';');
                }
                // Quoted because of the ':' when there's a port.
                builder.Append("host=\"");
                builder.Append(context.Request.Host.ToUriComponent());
                builder.Append("\"");
            }
        }

        private void AppendFor(HttpContext context, StringBuilder builder)
        {
            if (_forFormat > NodeFormat.None)
            {
                if (builder.Length > 0)
                {
                    builder.Append(';');
                }
                builder.Append("for=");
                AppendNode(context.Connection.RemoteIpAddress, context.Connection.RemotePort, _forFormat, builder);
            }
        }

        private void AppendBy(HttpContext context, StringBuilder builder)
        {
            if (_byFormat > NodeFormat.None)
            {
                if (builder.Length > 0)
                {
                    builder.Append(';');
                }
                builder.Append("by=");
                AppendNode(context.Connection.LocalIpAddress, context.Connection.LocalPort, _byFormat, builder);
            }
        }

        // https://tools.ietf.org/html/rfc7239#section-6
        private void AppendNode(IPAddress ipAddress, int port, NodeFormat format, StringBuilder builder)
        {
            // "It is important to note that an IPv6 address and any nodename with
            // node-port specified MUST be quoted, since ":" is not an allowed
            // character in "token"."
            var addPort = port != 0 && (format == NodeFormat.IpAndPort || format == NodeFormat.UnknownAndPort || format == NodeFormat.RandomAndPort);
            var ipv6 = (format == NodeFormat.Ip || format == NodeFormat.IpAndPort)
                && ipAddress != null && ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            var quote = addPort || ipv6;

            if (quote)
            {
                builder.Append("\"");
            }

            switch (format)
            {
                case NodeFormat.Ip:
                case NodeFormat.IpAndPort:
                    if (ipAddress != null)
                    {
                        if (ipv6)
                        {
                            builder.Append("[");
                        }
                        builder.Append(ipAddress.ToString());
                        if (ipv6)
                        {
                            builder.Append("]");
                        }
                        break;
                    }
                    // This primarily happens in test environments that don't use real connections.
                    goto case NodeFormat.Unknown;
                case NodeFormat.Unknown:
                case NodeFormat.UnknownAndPort:
                    builder.Append("unknown");
                    break;
                case NodeFormat.Random:
                case NodeFormat.RandomAndPort:
                    AppendRandom(builder);
                    break;
                default:
                    throw new NotImplementedException(format.ToString());
            }

            if (addPort)
            {
                builder.Append(":");
                builder.Append(port);
            }

            if (quote)
            {
                builder.Append("\"");
            }
        }

        // https://tools.ietf.org/html/rfc7239#section-6.3
        private void AppendRandom(StringBuilder builder)
        {
            var random = _randomFactory.CreateRandomInstance();
            builder.Append('_');
            // This length is arbitrary.
            for (var i = 0; i < 9; i++)
            {
                builder.Append(ObfChars[random.Next(ObfChars.Length)]);
            }
        }

        // For and By entries
        public enum NodeFormat
        {
            None,
            Random,
            RandomAndPort,
            Unknown,
            UnknownAndPort,
            Ip,
            IpAndPort,
        }
    }
}
