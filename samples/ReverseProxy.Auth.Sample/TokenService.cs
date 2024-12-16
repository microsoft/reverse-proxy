// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Yarp.Sample
{
    internal sealed class TokenService
    {
        internal Task<string> GetAuthTokenAsync(ClaimsPrincipal user)
        {
            // we only have tokens for bob
            if (string.Equals("Bob", user.Identity.Name))
            {
                return Task.FromResult(Guid.NewGuid().ToString());
            }
            return Task.FromResult<string>(null);
        }
    }
}
