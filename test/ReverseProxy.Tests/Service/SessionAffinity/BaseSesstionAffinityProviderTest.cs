// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.RuntimeModel;
using Tests.Common;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    public class BaseSesstionAffinityProviderTest : TestAutoMockBase
    {
        private class SessionAffinityProviderStub : BaseSessionAffinityProvider<string>
        {
            public static readonly string AffinityKeyItemName = "StubAffinityKey";

            public SessionAffinityProviderStub(IDataProtectionProvider dataProtectionProvider, ILogger logger)
                : base(dataProtectionProvider, logger)
            {}

            public override string Mode => "Stub";

            protected override string GetDestinationAffinityKey(DestinationInfo destination)
            {
                return destination.DestinationId;
            }

            protected override (string Key, bool ExtractedSuccessfully) GetRequestAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options)
            {
                return (Key: (string)context.Items[AffinityKeyItemName], ExtractedSuccessfully: true);
            }

            protected override void SetAffinityKey(HttpContext context, in BackendConfig.BackendSessionAffinityOptions options, string unencryptedKey)
            {
                context.Items[AffinityKeyItemName] = unencryptedKey;
            }
        }
    }
}
