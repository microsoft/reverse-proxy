// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Threading.Tasks;
#if NET7_0_OR_GREATER
using Microsoft.AspNetCore.RateLimiting;
#endif
using Microsoft.Extensions.Options;

namespace Yarp.ReverseProxy.Configuration;

// TODO: update this once AspNetCore provides a mechanism to validate the RateLimiter policies https://github.com/dotnet/aspnetcore/issues/45684

#if NET7_0_OR_GREATER

internal interface IYarpRateLimiterPolicyProvider
{
    ValueTask<object?> GetPolicyAsync(string policyName);
}

internal class YarpRateLimiterPolicyProvider : IYarpRateLimiterPolicyProvider
{
    private readonly RateLimiterOptions _rateLimiterOptions;

    private readonly System.Collections.IDictionary _policyMap, _unactivatedPolicyMap;

    public YarpRateLimiterPolicyProvider(IOptions<RateLimiterOptions> rateLimiterOptions)
    {
        _rateLimiterOptions = rateLimiterOptions?.Value ?? throw new ArgumentNullException(nameof(rateLimiterOptions));

        var type = typeof(RateLimiterOptions);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        _policyMap = (System.Collections.IDictionary)type.GetProperty("PolicyMap", flags)!.GetValue(_rateLimiterOptions, null)!;
        _unactivatedPolicyMap = (System.Collections.IDictionary)type.GetProperty("UnactivatedPolicyMap", flags)!.GetValue(_rateLimiterOptions, null)!;
    }

    public ValueTask<object?> GetPolicyAsync(string policyName)
    {
        return ValueTask.FromResult(_policyMap[policyName] ?? _unactivatedPolicyMap[policyName]);
    }
}
#endif
