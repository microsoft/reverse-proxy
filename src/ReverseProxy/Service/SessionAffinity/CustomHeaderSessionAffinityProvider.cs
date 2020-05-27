// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class CustomHeaderSessionAffinityProvider : BaseSessionAffinityProvider<string>
    {
        private const string CustomHeaderNameKey = "CustomHeaderName";

        public CustomHeaderSessionAffinityProvider(
            IDataProtectionProvider dataProtectionProvider,
            IEnumerable<IMissingDestinationHandler> missingDestinationHandlers,
            ILogger<CustomHeaderSessionAffinityProvider> logger)
            : base(dataProtectionProvider, missingDestinationHandlers, logger)
        {}

        public override string Mode => SessionAffinityBuiltIns.Modes.CustomHeander;

        protected override string GetDestinationAffinityKey(DestinationInfo destination)
        {
            return destination.DestinationId;
        }

        protected override string GetRequestAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options)
        {
            var customHeaderName = GetSettingValue(CustomHeaderNameKey, options);
            var keyHeaderValues = context.Request.Headers[customHeaderName];
            var encryptedRequestKey = !StringValues.IsNullOrEmpty(keyHeaderValues) ? keyHeaderValues[0] : null; // We always take the first value of a custom header storing an affinity key
            return encryptedRequestKey != null ? DataProtector.Unprotect(encryptedRequestKey) : null;
        }

        protected override void SetAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options, string unencryptedKey)
        {
            var customHeaderName = GetSettingValue(CustomHeaderNameKey, options);
            context.Response.Headers.Append(customHeaderName, DataProtector.Protect(unencryptedKey));
        }
    }
}
