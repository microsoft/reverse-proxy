// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Sample;

internal class TokenService
{
    internal Task<string> GetAuthTokenAsync(ClaimsPrincipal user)
    {
        return Task.FromResult(user.Identity.Name);
    }
}
