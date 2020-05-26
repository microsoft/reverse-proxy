// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
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

            public SessionAffinityProviderStub(IDataProtectionProvider dataProtectionProvider, IEnumerable<IMissingDestinationHandler> missingDestinationHandlers, ILogger logger)
                : base(dataProtectionProvider, missingDestinationHandlers, logger)
            {}

            public override string Mode => "Stub";

            protected override string GetDestinationAffinityKey(DestinationInfo destination)
            {
                return destination.DestinationId;
            }

            protected override string GetRequestAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options)
            {
                return (string)context.Items[AffinityKeyItemName];
            }

            protected override void SetAffinityKey(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, string unencryptedKey)
            {
                context.Items[AffinityKeyItemName] = unencryptedKey;
            }
        }
    }
}
