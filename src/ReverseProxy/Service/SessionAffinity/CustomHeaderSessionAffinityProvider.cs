// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class CustomHeaderSessionAffinityProvider : BaseSessionAffinityProvider<string>
    {
        private const string CustomHeaderNameKey = "CustomHeaderName";

        public override string Mode => "CustomHeader";

        //TBD. Add logging.

        protected override string GetDestinationAffinityKey(DestinationInfo destination)
        {
            return destination.DestinationId;
        }

        protected override string GetRequestAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options)
        {
            var customHeaderName = GetSettingValue(CustomHeaderNameKey, options);
            var keyHeaderValues = context.Request.Headers[customHeaderName];
            return !StringValues.IsNullOrEmpty(keyHeaderValues) ? keyHeaderValues[0] : null; // We always take the first value of a custom header storing an affinity key
        }

        protected override void SetEncryptedAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, string encryptedKey)
        {
            var cookieName = GetSettingValue(CustomHeaderNameKey, options);
            // TBD. The affinity key must be encrypted.
            context.Response.Cookies.Append(cookieName, encryptedKey);
        }
    }
}
