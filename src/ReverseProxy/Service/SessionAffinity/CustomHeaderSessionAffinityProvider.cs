// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    internal sealed class CustomHeaderSessionAffinityProvider : BaseSessionAffinityProvider<string>
    {
        private readonly ConditionalWeakTable<string, string> _defaultKeyNames = new ConditionalWeakTable<string, string>();
        public static readonly string DefaultCustomHeaderName = "X-Yarp-Affinity";

        public CustomHeaderSessionAffinityProvider(
            IDataProtectionProvider dataProtectionProvider,
            ILogger<CustomHeaderSessionAffinityProvider> logger)
            : base(dataProtectionProvider, logger)
        {}

        public override string Mode => SessionAffinityConstants.Modes.CustomHeader;

        protected override string GetDestinationAffinityKey(DestinationState destination)
        {
            return destination.DestinationId;
        }

        protected override (string? Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, SessionAffinityConfig config, string clusterId)
        {
            var customHeaderName = config.AffinityKeyName ?? GetUniqueDefaultKeyName(clusterId);

            var keyHeaderValues = context.Request.Headers[customHeaderName];

            if (StringValues.IsNullOrEmpty(keyHeaderValues))
            {
                // It means affinity key is not defined that is a successful case
                return (Key: null, ExtractedSuccessfully: true);
            }

            if (keyHeaderValues.Count > 1)
            {
                // Multiple values is an ambiguous case which is considered a key extraction failure
                Log.RequestAffinityHeaderHasMultipleValues(Logger, customHeaderName, keyHeaderValues.Count);
                return (Key: null, ExtractedSuccessfully: false);
            }

            return Unprotect(keyHeaderValues[0]);
        }

        protected override void SetAffinityKey(HttpContext context, SessionAffinityConfig config, string unencryptedKey, string clusterId)
        {
            context.Response.Headers.Append(config.AffinityKeyName ?? GetUniqueDefaultKeyName(clusterId), Protect(unencryptedKey));
        }

        private string GetUniqueDefaultKeyName(string clusterId)
        {
            return _defaultKeyNames.GetValue(clusterId, i => $"{DefaultCustomHeaderName}_{GetDefaultKeyNameSuffix(i)}");
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, int, Exception?> _requestAffinityHeaderHasMultipleValues = LoggerMessage.Define<string, int>(
                LogLevel.Error,
                EventIds.RequestAffinityHeaderHasMultipleValues,
                "The request affinity header `{headerName}` has `{valueCount}` values.");

            public static void RequestAffinityHeaderHasMultipleValues(ILogger logger, string headerName, int valueCount)
            {
                _requestAffinityHeaderHasMultipleValues(logger, headerName, valueCount, null);
            }
        }
    }
}
