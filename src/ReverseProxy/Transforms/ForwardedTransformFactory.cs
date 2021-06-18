// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Transforms
{
    internal sealed class ForwardedTransformFactory : ITransformFactory
    {
        internal static readonly string XForwardedKey = "X-Forwarded";
        internal static readonly string ForwardedKey = "Forwarded";
        internal static readonly string AppendKey = "Append";
        internal static readonly string PrefixForwardedKey = "Prefix";
        internal static readonly string ForKey =  "For";
        internal static readonly string ByKey = "By";
        internal static readonly string HostKey = "Host";
        internal static readonly string ProtoKey = "Proto";
        internal static readonly string PrefixKey = "Prefix";
        internal static readonly string ForFormatKey = "ForFormat";
        internal static readonly string ByFormatKey = "ByFormat";
        internal static readonly string ClientCertKey = "ClientCert";

        private readonly IRandomFactory _randomFactory;

        public ForwardedTransformFactory(IRandomFactory randomFactory)
        {
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
        }

        public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue(XForwardedKey, out var headerValue))
            {
                var expected = 1;

                expected++;
                if (!Enum.TryParse<ForwardedTransformActions>(headerValue, out var _))
                {
                    context.Errors.Add(new ArgumentException($"Unexpected value for {XForwardedKey}: {headerValue}. Expected one of {nameof(ForwardedTransformActions)}"));
                }

                var prefix = "X-Forwarded-";
                if (transformValues.TryGetValue(PrefixForwardedKey, out var prefixValue))
                {
                    expected++;
                    prefix = prefixValue;
                }

                if (transformValues.TryGetValue(prefix + ForKey, out headerValue))
                {
                    expected++;
                    if (!Enum.TryParse<ForwardedTransformActions>(headerValue, out var _))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for {prefix + ForKey}: {headerValue}. Expected one of {nameof(ForwardedTransformActions)}"));
                    }
                }

                if (transformValues.TryGetValue(prefix + PrefixKey, out headerValue))
                {
                    expected++;
                    if (!Enum.TryParse<ForwardedTransformActions>(headerValue, out var _))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for {prefix + PrefixKey}: {headerValue}. Expected one of {nameof(ForwardedTransformActions)}"));
                    }
                }

                if (transformValues.TryGetValue(prefix + HostKey, out headerValue))
                {
                    expected++;
                    if (!Enum.TryParse<ForwardedTransformActions>(headerValue, out var _))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for {prefix + HostKey}: {headerValue}. Expected one of {nameof(ForwardedTransformActions)}"));
                    }
                }

                if (transformValues.TryGetValue(prefix + PrefixKey, out headerValue))
                {
                    expected++;
                    if (!Enum.TryParse<ForwardedTransformActions>(headerValue, out var _))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for {prefix + PrefixKey}: {headerValue}. Expected one of {nameof(ForwardedTransformActions)}"));
                    }
                }

                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected);
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
                    if (!Enum.TryParse<NodeFormat>(forFormat, ignoreCase: true, out var _))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for Forwarded:ForFormat: {forFormat}. Expected: {enumValues}"));
                    }
                }

                if (transformValues.TryGetValue(ByFormatKey, out var byFormat))
                {
                    expected++;
                    if (!Enum.TryParse<NodeFormat>(byFormat, ignoreCase: true, out var _))
                    {
                        context.Errors.Add(new ArgumentException($"Unexpected value for Forwarded:ByFormat: {byFormat}. Expected: {enumValues}"));
                    }
                }

                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected);

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
                TransformHelpers.TryCheckTooManyParameters(context, transformValues, expected: 1);
            }
            else
            {
                return false;
            }

            return true;
        }

        public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            var xExpected = 0;

            var defaultXAction = ForwardedTransformActions.Set;
            if (transformValues.TryGetValue(XForwardedKey, out var headerValue)
                && Enum.TryParse<ForwardedTransformActions>(headerValue, out var action))
            {
                xExpected++;
                defaultXAction = action;
            }

            var prefix = "X-Forwarded-";
            if (transformValues.TryGetValue(PrefixForwardedKey, out var prefixValue))
            {
                xExpected++;
                prefix = prefixValue;
            }

            var xForAction = defaultXAction;
            if (transformValues.TryGetValue(prefix + ForKey, out headerValue)
                && Enum.TryParse<ForwardedTransformActions>(headerValue, out action))
            {
                xExpected++;
                xForAction = action;
            }

            var xPrefixAction = defaultXAction;
            if (transformValues.TryGetValue(prefix + PrefixKey, out headerValue)
                && Enum.TryParse(headerValue, out action))
            {
                xExpected++;
                xPrefixAction = action;
            }

            var xHostAction = defaultXAction;
            if (transformValues.TryGetValue(prefix + HostKey, out headerValue)
                && Enum.TryParse(headerValue, out action))
            {
                xExpected++;
                xHostAction = action;
            }

            var xProtoAction = defaultXAction;
            if (transformValues.TryGetValue(prefix + ProtoKey, out headerValue)
                && Enum.TryParse(headerValue, out action))
            {
                xExpected++;
                xProtoAction = action;
            }

            if (xExpected > 0)
            {
                TransformHelpers.CheckTooManyParameters(transformValues, xExpected);

                context.AddXForwardedFor(prefix, xForAction);
                context.AddXForwardedPrefix(prefix, xPrefixAction);
                context.AddXForwardedHost(prefix, xHostAction);
                context.AddXForwardedProto(prefix, xProtoAction);
            }
            else if (transformValues.TryGetValue(ForwardedKey, out var forwardedHeader))
            {
                var useHost = false;
                var useProto = false;
                var useFor = false;
                var useBy = false;
                var forFormat = NodeFormat.None;
                var byFormat = NodeFormat.None;

                // for, host, proto, Prefix
                var tokens = forwardedHeader.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var token in tokens)
                {
                    if (string.Equals(token, ForKey, StringComparison.OrdinalIgnoreCase))
                    {
                        useFor = true;
                        forFormat = NodeFormat.Random; // RFC Default
                    }
                    else if (string.Equals(token, ByKey, StringComparison.OrdinalIgnoreCase))
                    {
                        useBy = true;
                        byFormat = NodeFormat.Random; // RFC Default
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
                        throw new ArgumentException($"Unexpected value for Forwarded: {token}. Expected 'for', 'host', 'proto', or 'by'");
                    }
                }

                var expected = 1;

                var append = false;
                if (transformValues.TryGetValue(AppendKey, out var appendValue))
                {
                    expected++;
                    append = bool.Parse(appendValue);
                }

                if (useFor && transformValues.TryGetValue(ForFormatKey, out var forFormatString))
                {
                    expected++;
                    forFormat = Enum.Parse<NodeFormat>(forFormatString, ignoreCase: true);
                }

                if (useBy && transformValues.TryGetValue(ByFormatKey, out var byFormatString))
                {
                    expected++;
                    byFormat = Enum.Parse<NodeFormat>(byFormatString, ignoreCase: true);
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
