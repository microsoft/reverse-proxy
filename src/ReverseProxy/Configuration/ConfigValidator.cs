// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Http.Timeouts;
#endif
#if NET8_0_OR_GREATER
using Microsoft.Extensions.Options;
#endif
using Yarp.ReverseProxy.Configuration.ClusterValidators;
using Yarp.ReverseProxy.Configuration.RouteValidators;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Configuration;

internal sealed class ConfigValidator : IConfigValidator
{
    private readonly ITransformBuilder _transformBuilder;
    private readonly IAuthorizationPolicyProvider _authorizationPolicyProvider;
    private readonly IYarpRateLimiterPolicyProvider _rateLimiterPolicyProvider;
    private readonly ICorsPolicyProvider _corsPolicyProvider;
#if NET8_0_OR_GREATER
    private readonly IOptionsMonitor<RequestTimeoutOptions> _timeoutOptions;
#endif
    private readonly List<IRouteValidator> _routeValidators;
    private readonly List<IClusterValidator> _clusterValidators;
    
    public ConfigValidator(ITransformBuilder transformBuilder,
        IAuthorizationPolicyProvider authorizationPolicyProvider,
        IYarpRateLimiterPolicyProvider rateLimiterPolicyProvider,
        ICorsPolicyProvider corsPolicyProvider,
#if NET8_0_OR_GREATER
        IOptionsMonitor<RequestTimeoutOptions> timeoutOptions,
#endif
        IEnumerable<IRouteValidator> routeValidators,
        IEnumerable<IClusterValidator> clusterValidators)
    {
        _transformBuilder = transformBuilder ?? throw new ArgumentNullException(nameof(transformBuilder));
        _authorizationPolicyProvider = authorizationPolicyProvider ?? throw new ArgumentNullException(nameof(authorizationPolicyProvider));
        _rateLimiterPolicyProvider = rateLimiterPolicyProvider ?? throw new ArgumentNullException(nameof(rateLimiterPolicyProvider));
        _corsPolicyProvider = corsPolicyProvider ?? throw new ArgumentNullException(nameof(corsPolicyProvider));
#if NET8_0_OR_GREATER
        _timeoutOptions = timeoutOptions ?? throw new ArgumentNullException(nameof(timeoutOptions));
#endif
        _routeValidators = routeValidators;
        _clusterValidators = clusterValidators;
    }

    // Note this performs all validation steps without short circuiting in order to report all possible errors.
    public async ValueTask<IList<Exception>> ValidateRouteAsync(RouteConfig route)
    {
        _ = route ?? throw new ArgumentNullException(nameof(route));
        var errors = new List<Exception>();

        if (string.IsNullOrEmpty(route.RouteId))
        {
            errors.Add(new ArgumentException("Missing Route Id."));
        }

        errors.AddRange(_transformBuilder.ValidateRoute(route));
        await ValidateAuthorizationPolicyAsync(errors, route.AuthorizationPolicy, route.RouteId);
#if NET7_0_OR_GREATER
        await ValidateRateLimiterPolicyAsync(errors, route.RateLimiterPolicy, route.RouteId);
#endif
#if NET8_0_OR_GREATER
        ValidateTimeoutPolicy(errors, route.TimeoutPolicy, route.Timeout, route.RouteId);
#endif
        await ValidateCorsPolicyAsync(errors, route.CorsPolicy, route.RouteId);

        if (route.Match is null)
        {
            errors.Add(new ArgumentException($"Route '{route.RouteId}' did not set any match criteria, it requires Hosts or Path specified. Set the Path to '/{{**catchall}}' to match all requests."));
            return errors;
        }

        if ((route.Match.Hosts is null || route.Match.Hosts.All(string.IsNullOrEmpty)) && string.IsNullOrEmpty(route.Match.Path))
        {
            errors.Add(new ArgumentException($"Route '{route.RouteId}' requires Hosts or Path specified. Set the Path to '/{{**catchall}}' to match all requests."));
        }

        foreach (var routeValidator in _routeValidators)
        {
            routeValidator.AddValidationErrors(route.Match, route.RouteId, errors);
        }

        return errors;
    }

    // Note this performs all validation steps without short circuiting in order to report all possible errors.
    public ValueTask<IList<Exception>> ValidateClusterAsync(ClusterConfig cluster)
    {
        _ = cluster ?? throw new ArgumentNullException(nameof(cluster));
        var errors = new List<Exception>();

        if (string.IsNullOrEmpty(cluster.ClusterId))
        {
            errors.Add(new ArgumentException("Missing Cluster Id."));
        }

        errors.AddRange(_transformBuilder.ValidateCluster(cluster));

        foreach (var clusterValidator in _clusterValidators)
        {
            clusterValidator.AddValidationErrors(cluster, errors);
        }

        return new ValueTask<IList<Exception>>(errors);
    }

    private async ValueTask ValidateAuthorizationPolicyAsync(IList<Exception> errors, string? authorizationPolicyName, string routeId)
    {
        if (string.IsNullOrEmpty(authorizationPolicyName))
        {
            return;
        }

        if (string.Equals(AuthorizationConstants.Default, authorizationPolicyName, StringComparison.OrdinalIgnoreCase))
        {
            var policy = await _authorizationPolicyProvider.GetPolicyAsync(authorizationPolicyName);
            if (policy is not null)
            {
                errors.Add(new ArgumentException($"The application has registered an authorization policy named '{authorizationPolicyName}' that conflicts with the reserved authorization policy name used on this route. The registered policy name needs to be changed for this route to function."));
            }
            return;
        }

        if (string.Equals(AuthorizationConstants.Anonymous, authorizationPolicyName, StringComparison.OrdinalIgnoreCase))
        {
            var policy = await _authorizationPolicyProvider.GetPolicyAsync(authorizationPolicyName);
            if (policy is not null)
            {
                errors.Add(new ArgumentException($"The application has registered an authorization policy named '{authorizationPolicyName}' that conflicts with the reserved authorization policy name used on this route. The registered policy name needs to be changed for this route to function."));
            }
            return;
        }

        try
        {
            var policy = await _authorizationPolicyProvider.GetPolicyAsync(authorizationPolicyName);
            if (policy is null)
            {
                errors.Add(new ArgumentException($"Authorization policy '{authorizationPolicyName}' not found for route '{routeId}'."));
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ArgumentException($"Unable to retrieve the authorization policy '{authorizationPolicyName}' for route '{routeId}'.", ex));
        }
    }
#if NET8_0_OR_GREATER
    private void ValidateTimeoutPolicy(IList<Exception> errors, string? timeoutPolicyName, TimeSpan? timeout, string routeId)
    {
        if (!string.IsNullOrEmpty(timeoutPolicyName))
        {
            var policies = _timeoutOptions.CurrentValue.Policies;

            if (string.Equals(TimeoutPolicyConstants.Disable, timeoutPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                if (policies.TryGetValue(timeoutPolicyName, out var _))
                {
                    errors.Add(new ArgumentException($"The application has registered a timeout policy named '{timeoutPolicyName}' that conflicts with the reserved timeout policy name used on this route. The registered policy name needs to be changed for this route to function."));
                }
            }
            else if (!policies.TryGetValue(timeoutPolicyName, out var _))
            {
                errors.Add(new ArgumentException($"Timeout policy '{timeoutPolicyName}' not found for route '{routeId}'."));
            }

            if (timeout.HasValue)
            {
                errors.Add(new ArgumentException($"Route '{routeId}' has both a Timeout '{timeout}' and TimeoutPolicy '{timeoutPolicyName}'."));
            }
        }

        if (timeout.HasValue && timeout.Value.TotalMilliseconds <= 0)
        {
            errors.Add(new ArgumentException($"The Timeout value '{timeout.Value}' is invalid for route '{routeId}'. The Timeout must be greater than zero milliseconds."));
        }
    }
#endif
    private async ValueTask ValidateRateLimiterPolicyAsync(IList<Exception> errors, string? rateLimiterPolicyName, string routeId)
    {
        if (string.IsNullOrEmpty(rateLimiterPolicyName))
        {
            return;
        }

        if (string.Equals(RateLimitingConstants.Default, rateLimiterPolicyName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(RateLimitingConstants.Disable, rateLimiterPolicyName, StringComparison.OrdinalIgnoreCase))
        {
            var policy = await _rateLimiterPolicyProvider.GetPolicyAsync(rateLimiterPolicyName);
            if (policy is not null)
            {
                // We weren't expecting to find a policy with these names.
                errors.Add(new ArgumentException($"The application has registered a RateLimiter policy named '{rateLimiterPolicyName}' that conflicts with the reserved RateLimiter policy name used on this route. The registered policy name needs to be changed for this route to function."));
            }
            return;
        }

        try
        {
            var policy = await _rateLimiterPolicyProvider.GetPolicyAsync(rateLimiterPolicyName);

            if (policy is null)
            {
                errors.Add(new ArgumentException($"RateLimiter policy '{rateLimiterPolicyName}' not found for route '{routeId}'."));
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ArgumentException($"Unable to retrieve the RateLimiter policy '{rateLimiterPolicyName}' for route '{routeId}'.", ex));
        }
    }

    private async ValueTask ValidateCorsPolicyAsync(IList<Exception> errors, string? corsPolicyName, string routeId)
    {
        if (string.IsNullOrEmpty(corsPolicyName))
        {
            return;
        }

        if (string.Equals(CorsConstants.Default, corsPolicyName, StringComparison.OrdinalIgnoreCase))
        {
            var dummyHttpContext = new DefaultHttpContext();
            var policy = await _corsPolicyProvider.GetPolicyAsync(dummyHttpContext, corsPolicyName);
            if (policy is not null)
            {
                errors.Add(new ArgumentException($"The application has registered a CORS policy named '{corsPolicyName}' that conflicts with the reserved CORS policy name used on this route. The registered policy name needs to be changed for this route to function."));
            }
            return;
        }

        if (string.Equals(CorsConstants.Disable, corsPolicyName, StringComparison.OrdinalIgnoreCase))
        {
            var dummyHttpContext = new DefaultHttpContext();
            var policy = await _corsPolicyProvider.GetPolicyAsync(dummyHttpContext, corsPolicyName);
            if (policy is not null)
            {
                errors.Add(new ArgumentException($"The application has registered a CORS policy named '{corsPolicyName}' that conflicts with the reserved CORS policy name used on this route. The registered policy name needs to be changed for this route to function."));
            }
            return;
        }

        try
        {
            var dummyHttpContext = new DefaultHttpContext();
            var policy = await _corsPolicyProvider.GetPolicyAsync(dummyHttpContext, corsPolicyName);
            if (policy is null)
            {
                errors.Add(new ArgumentException($"CORS policy '{corsPolicyName}' not found for route '{routeId}'."));
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ArgumentException($"Unable to retrieve the CORS policy '{corsPolicyName}' for route '{routeId}'.", ex));
        }
    }
}
