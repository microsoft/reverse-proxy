// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET7_0_OR_GREATER
using System;
using System.Threading.Tasks;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Yarp.ReverseProxy.Configuration;

public class YarpRateLimiterPolicyProviderTests
{
    [Fact]
    public async Task GetPolicyAsync_Works()
    {
        var services = new ServiceCollection();

        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("customPolicy", opt =>
            {
                opt.PermitLimit = 4;
                opt.Window = TimeSpan.FromSeconds(12);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 2;
            });
        });

        services.AddReverseProxy();
        var provider = services.BuildServiceProvider();
        var rateLimiterPolicyProvider = provider.GetRequiredService<IYarpRateLimiterPolicyProvider>();
        Assert.Null(await rateLimiterPolicyProvider.GetPolicyAsync("anotherPolicy"));
        Assert.NotNull(await rateLimiterPolicyProvider.GetPolicyAsync("customPolicy"));
    }
}
#endif
