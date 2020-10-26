// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class CustomHeaderSessionAffinityProvider : BaseSessionAffinityProvider<string>
    {
        public static readonly string DefaultCustomHeaderName = "X-Microsoft-Proxy-Affinity";
        private const string CustomHeaderNameKey = "CustomHeaderName";

        public CustomHeaderSessionAffinityProvider(
            IDataProtectionProvider dataProtectionProvider,
            ILogger<CustomHeaderSessionAffinityProvider> logger)
            : base(dataProtectionProvider, logger)
        {}

        public override string Mode => SessionAffinityConstants.Modes.CustomHeader;

        protected override string GetDestinationAffinityKey(DestinationInfo destination)
        {
            return destination.DestinationId;
        }

        protected override (string Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, in ClusterSessionAffinityOptions options)
        {
            var customHeaderName = options.Settings != null && options.Settings.TryGetValue(CustomHeaderNameKey, out var nameInSettings) ? nameInSettings : DefaultCustomHeaderName;
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

        protected override void SetAffinityKey(HttpContext context, in ClusterSessionAffinityOptions options, string unencryptedKey)
        {
            var customHeaderName = GetSettingValue(CustomHeaderNameKey, options);
            context.Response.Headers.Append(customHeaderName, Protect(unencryptedKey));
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, int, Exception> _requestAffinityHeaderHasMultipleValues = LoggerMessage.Define<string, int>(
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
