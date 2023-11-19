// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Configuration.ClusterValidators;
using Yarp.ReverseProxy.Configuration.RouteValidators;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Configuration;

internal sealed class ConfigValidator : IConfigValidator
{
    private readonly ITransformBuilder _transformBuilder;
    private readonly ICorsPolicyProvider _corsPolicyProvider;

    private readonly IEnumerable<IRouteValidator> _routeValidators;
    private readonly IEnumerable<IClusterValidator> _clusterValidators;
    public ConfigValidator(ITransformBuilder transformBuilder,
         ICorsPolicyProvider corsPolicyProvider,
        IEnumerable<IRouteValidator> routeValidators,
        IEnumerable<IClusterValidator> clusterValidators)
    {
        _transformBuilder = transformBuilder ?? throw new ArgumentNullException(nameof(transformBuilder));
         _corsPolicyProvider = corsPolicyProvider ?? throw new ArgumentNullException(nameof(corsPolicyProvider));
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
           await routeValidator.ValidateAsync(route, errors);
        }

        return errors;
    }

    // Note this performs all validation steps without short circuiting in order to report all possible errors.
    public async ValueTask<IList<Exception>> ValidateClusterAsync(ClusterConfig cluster)
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
           await clusterValidator.ValidateAsync(cluster, errors);
        }

        return errors;
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
