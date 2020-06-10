// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class CustomHeaderSessionAffinityProvider : BaseSessionAffinityProvider<string>
    {
        private readonly CustomHeaderSessionAffinityProviderOptions _providerOptions;

        public CustomHeaderSessionAffinityProvider(
            IOptions<CustomHeaderSessionAffinityProviderOptions> providerOptions,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<CustomHeaderSessionAffinityProvider> logger)
            : base(dataProtectionProvider, logger)
        {
            _providerOptions = providerOptions?.Value ?? throw new ArgumentNullException(nameof(providerOptions));
        }

        public override string Mode => SessionAffinityConstants.Modes.CustomHeader;

        protected override string GetDestinationAffinityKey(DestinationInfo destination)
        {
            return destination.DestinationId;
        }

        protected override (string Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options)
        {
            var keyHeaderValues = context.Request.Headers[_providerOptions.CustomHeaderName];

            if (StringValues.IsNullOrEmpty(keyHeaderValues))
            {
                // It means affinity key is not defined that is a successful case
                return (Key: null, ExtractedSuccessfully: true);
            }

            if (keyHeaderValues.Count > 1)
            {
                // Multiple values is an ambiguous case which is considered a key extraction failure
                Log.RequestAffinityHeaderHasMultipleValues(Logger, _providerOptions.CustomHeaderName, keyHeaderValues.Count);
                return (Key: null, ExtractedSuccessfully: false);
            }

            return Unprotect(keyHeaderValues[0]);
        }

        protected override void SetAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options, string unencryptedKey)
        {
            context.Response.Headers.Append(_providerOptions.CustomHeaderName, Protect(unencryptedKey));
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
