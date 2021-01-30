// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.Config
{
    internal class ForwardedTransformFactory : ITransformFactory
    {
        internal static readonly string XForwardedKey = "X-Forwarded";
        internal static readonly string ForwardedKey = "Forwarded";
        internal static readonly string AppendKey = "Append";
        internal static readonly string PrefixKey = "Prefix";
        internal static readonly string ForKey = "For";
        internal static readonly string ByKey = "By";
        internal static readonly string HostKey = "Host";
        internal static readonly string ProtoKey = "Proto";
        internal static readonly string PathBaseKey = "PathBase";
        internal static readonly string ForFormatKey = "ForFormat";
        internal static readonly string ByFormatKey = "ByFormat";
        internal static readonly string ClientCertKey = "ClientCert";

        private readonly IRandomFactory _randomFactory;

        internal ForwardedTransformFactory(IRandomFactory randomFactory)
        {
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
        }

        public bool Validate(TransformValidationContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(XForwardedKey, out var xforwardedHeaders))
            {
                var expected = 1;

                if (transformValues.TryGetValue(AppendKey, out var appendValue))
                {
                    expected++;
                    if (!bool.TryParse(appendValue, out var _))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for X-Forwarded:Append: {appendValue}. Expected 'true' or 'false'"));
                    }
                }

                if (transformValues.TryGetValue(PrefixKey, out var _))
                {
                    expected++;
                }

                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected);

                // for, host, proto, PathBase
                var tokens = xforwardedHeaders.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var token in tokens)
                {
                    if (!string.Equals(token, ForKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(token, HostKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(token, ProtoKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(token, PathBaseKey, StringComparison.OrdinalIgnoreCase))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for X-Forwarded: {token}. Expected 'for', 'host', 'proto', or 'PathBase'"));
                    }
                }
            }
            else if (transformValues.TryGetValue(ForwardedKey, out var forwardedHeader))
            {
                var expected = 1;

                if (transformValues.TryGetValue(AppendKey, out var appendValue))
                {
                    expected++;
                    if (!bool.TryParse(appendValue, out var _))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for Forwarded:Append: {appendValue}. Expected 'true' or 'false'"));
                    }
                }

                var enumValues = "Random,RandomAndPort,Unknown,UnknownAndPort,Ip,IpAndPort";
                if (transformValues.TryGetValue(ForFormatKey, out var forFormat))
                {
                    expected++;
                    if (!Enum.TryParse<RequestHeaderForwardedTransform.NodeFormat>(forFormat, ignoreCase: true, out var _))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for Forwarded:ForFormat: {forFormat}. Expected: {enumValues}"));
                    }
                }

                if (transformValues.TryGetValue(ByFormatKey, out var byFormat))
                {
                    expected++;
                    if (!Enum.TryParse<RequestHeaderForwardedTransform.NodeFormat>(byFormat, ignoreCase: true, out var _))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for Forwarded:ByFormat: {byFormat}. Expected: {enumValues}"));
                    }
                }

                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected);

                // for, host, proto, by
                var tokens = forwardedHeader.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var token in tokens)
                {
                    if (!string.Equals(token, ByKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(token, HostKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(token, ProtoKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(token, ForKey, StringComparison.OrdinalIgnoreCase))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for X-Forwarded: {token}. Expected 'for', 'host', 'proto', or 'by'"));
                    }
                }
            }
            else if (transformValues.TryGetValue(ClientCertKey, out var clientCertHeader))
            {
                TransformHelpers.TryCheckTooManyParameters(context.Errors.Add, transformValues, expected: 1);
            }
            else
            {
                return false;
            }

            return true;
        }

        public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(XForwardedKey, out var xforwardedHeaders))
            {
                var expected = 1;

                var append = true;
                if (transformValues.TryGetValue(AppendKey, out var appendValue))
                {
                    expected++;
                    append = bool.Parse(appendValue);
                }

                var prefix = "X-Forwarded-";
                if (transformValues.TryGetValue(PrefixKey, out var prefixValue))
                {
                    expected++;
                    prefix = prefixValue;
                }

                TransformHelpers.CheckTooManyParameters(transformValues, expected);

                // for, host, proto, PathBase
                var tokens = xforwardedHeaders.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                bool useFor, useHost, useProto, usePathBase;
                useFor = useHost = useProto = usePathBase = false;

                foreach (var token in tokens)
                {
                    if (string.Equals(token, ForKey, StringComparison.OrdinalIgnoreCase))
                    {
                        useFor = true;
                    }
                    else if (string.Equals(token, HostKey, StringComparison.OrdinalIgnoreCase))
                    {
                        useHost = true;
                    }
                    else if (string.Equals(token, ProtoKey, StringComparison.OrdinalIgnoreCase))
                    {
                        useProto = true;
                    }
                    else if (string.Equals(token, PathBaseKey, StringComparison.OrdinalIgnoreCase))
                    {
                        usePathBase = true;
                    }
                    else
                    {
                        throw new ArgumentException($"Unexpected value for X-Forwarded: {token}. Expected 'for', 'host', 'proto', or 'PathBase'");
                    }
                }

                context.AddXForwarded(prefix, useFor, useHost, useProto, usePathBase, append);
            }
            else if (transformValues.TryGetValue(ForwardedKey, out var forwardedHeader))
            {
                var useHost = false;
                var useProto = false;
                var useFor = false;
                var useBy = false;
                var forFormat = RequestHeaderForwardedTransform.NodeFormat.None;
                var byFormat = RequestHeaderForwardedTransform.NodeFormat.None;

                // for, host, proto, PathBase
                var tokens = forwardedHeader.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var token in tokens)
                {
                    if (string.Equals(token, ForKey, StringComparison.OrdinalIgnoreCase))
                    {
                        useFor = true;
                        forFormat = RequestHeaderForwardedTransform.NodeFormat.Random; // RFC Default
                    }
                    else if (string.Equals(token, ByKey, StringComparison.OrdinalIgnoreCase))
                    {
                        useBy = true;
                        byFormat = RequestHeaderForwardedTransform.NodeFormat.Random; // RFC Default
                    }
                    else if (string.Equals(token, HostKey, StringComparison.OrdinalIgnoreCase))
                    {
                        useHost = true;
                    }
                    else if (string.Equals(token, ProtoKey, StringComparison.OrdinalIgnoreCase))
                    {
                        useProto = true;
                    }
                    else
                    {
                        throw new ArgumentException($"Unexpected value for X-Forwarded: {token}. Expected 'for', 'host', 'proto', or 'by'");
                    }
                }

                var expected = 1;

                var append = true;
                if (transformValues.TryGetValue(AppendKey, out var appendValue))
                {
                    expected++;
                    append = bool.Parse(appendValue);
                }

                if (useFor && transformValues.TryGetValue(ForFormatKey, out var forFormatString))
                {
                    expected++;
                    forFormat = Enum.Parse<RequestHeaderForwardedTransform.NodeFormat>(forFormatString, ignoreCase: true);
                }

                if (useBy && transformValues.TryGetValue(ByFormatKey, out var byFormatString))
                {
                    expected++;
                    byFormat = Enum.Parse<RequestHeaderForwardedTransform.NodeFormat>(byFormatString, ignoreCase: true);
                }

                TransformHelpers.CheckTooManyParameters(transformValues, expected);

                context.UseDefaultForwarders = false;
                if (useBy || useFor || useHost || useProto)
                {
                    // Not using the extension to avoid resolving the random factory each time.
                    context.RequestTransforms.Add(new RequestHeaderForwardedTransform(_randomFactory, forFormat, byFormat, useHost, useProto, append));
                }
            }
            else if (transformValues.TryGetValue(ClientCertKey, out var clientCertHeader))
            {
                TransformHelpers.CheckTooManyParameters(transformValues, expected: 1);
                context.AddClientCertHeader(clientCertHeader);
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
