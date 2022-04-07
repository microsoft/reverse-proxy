// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.SessionAffinity;
using Yarp.ReverseProxy.Transforms.Builder;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Configuration;

internal sealed class ConfigValidator : IConfigValidator
{
    private static readonly HashSet<string> _validMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "HEAD", "OPTIONS", "GET", "PUT", "POST", "PATCH", "DELETE", "TRACE",
    };

    private readonly ITransformBuilder _transformBuilder;
    private readonly IAuthorizationPolicyProvider _authorizationPolicyProvider;
    private readonly ICorsPolicyProvider _corsPolicyProvider;
    private readonly IDictionary<string, ILoadBalancingPolicy> _loadBalancingPolicies;
    private readonly IDictionary<string, IAffinityFailurePolicy> _affinityFailurePolicies;
    private readonly IDictionary<string, IAvailableDestinationsPolicy> _availableDestinationsPolicies;
    private readonly IDictionary<string, IActiveHealthCheckPolicy> _activeHealthCheckPolicies;
    private readonly IDictionary<string, IPassiveHealthCheckPolicy> _passiveHealthCheckPolicies;
    private readonly ILogger _logger;


    public ConfigValidator(ITransformBuilder transformBuilder,
        IAuthorizationPolicyProvider authorizationPolicyProvider,
        ICorsPolicyProvider corsPolicyProvider,
        IEnumerable<ILoadBalancingPolicy> loadBalancingPolicies,
        IEnumerable<IAffinityFailurePolicy> affinityFailurePolicies,
        IEnumerable<IAvailableDestinationsPolicy> availableDestinationsPolicies,
        IEnumerable<IActiveHealthCheckPolicy> activeHealthCheckPolicies,
        IEnumerable<IPassiveHealthCheckPolicy> passiveHealthCheckPolicies,
        ILogger<ConfigValidator> logger)
    {
        _transformBuilder = transformBuilder ?? throw new ArgumentNullException(nameof(transformBuilder));
        _authorizationPolicyProvider = authorizationPolicyProvider ?? throw new ArgumentNullException(nameof(authorizationPolicyProvider));
        _corsPolicyProvider = corsPolicyProvider ?? throw new ArgumentNullException(nameof(corsPolicyProvider));
        _loadBalancingPolicies = loadBalancingPolicies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(loadBalancingPolicies));
        _affinityFailurePolicies = affinityFailurePolicies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(affinityFailurePolicies));
        _availableDestinationsPolicies = availableDestinationsPolicies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(availableDestinationsPolicies));
        _activeHealthCheckPolicies = activeHealthCheckPolicies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(activeHealthCheckPolicies));
        _passiveHealthCheckPolicies = passiveHealthCheckPolicies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(passiveHealthCheckPolicies));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        await ValidateCorsPolicyAsync(errors, route.CorsPolicy, route.RouteId);

        if (route.Match is null)
        {
            errors.Add(new ArgumentException($"Route '{route.RouteId}' did not set any match criteria, it requires Hosts or Path specified. Set the Path to '/{{**catchall}}' to match all requests."));
            return errors;
        }

        if ((route.Match.Hosts is null || !route.Match.Hosts.Any(host => !string.IsNullOrEmpty(host))) && string.IsNullOrEmpty(route.Match.Path))
        {
            errors.Add(new ArgumentException($"Route '{route.RouteId}' requires Hosts or Path specified. Set the Path to '/{{**catchall}}' to match all requests."));
        }

        ValidateHost(errors, route.Match.Hosts, route.RouteId);
        ValidatePath(errors, route.Match.Path, route.RouteId);
        ValidateMethods(errors, route.Match.Methods, route.RouteId);
        ValidateHeaders(errors, route.Match.Headers, route.RouteId);
        ValidateQueryParameters(errors, route.Match.QueryParameters, route.RouteId);

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
        ValidateLoadBalancing(errors, cluster);
        ValidateSessionAffinity(errors, cluster);
        ValidateProxyHttpClient(errors, cluster);
        ValidateProxyHttpRequest(errors, cluster);
        ValidateHealthChecks(errors, cluster);

        return new ValueTask<IList<Exception>>(errors);
    }

    private static void ValidateHost(IList<Exception> errors, IReadOnlyList<string>? hosts, string routeId)
    {
        // Host is optional when Path is specified
        if (hosts is null || hosts.Count == 0)
        {
            return;
        }

        foreach (var host in hosts)
        {
            if (string.IsNullOrEmpty(host))
            {
                errors.Add(new ArgumentException($"Empty host name has been set for route '{routeId}'."));
            }
            else if (host.Contains("xn--", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ArgumentException($"Punycode host name '{host}' has been set for route '{routeId}'. Use the unicode host name instead."));
            }
        }
    }

    private static void ValidatePath(IList<Exception> errors, string? path, string routeId)
    {
        // Path is optional when Host is specified
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            RoutePatternFactory.Parse(path);
        }
        catch (RoutePatternException ex)
        {
            errors.Add(new ArgumentException($"Invalid path '{path}' for route '{routeId}'.", ex));
        }
    }

    private static void ValidateMethods(IList<Exception> errors, IReadOnlyList<string>? methods, string routeId)
    {
        // Methods are optional
        if (methods is null)
        {
            return;
        }

        var seenMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in methods)
        {
            if (!seenMethods.Add(method))
            {
                errors.Add(new ArgumentException($"Duplicate HTTP method '{method}' for route '{routeId}'."));
                continue;
            }

            if (!_validMethods.Contains(method))
            {
                errors.Add(new ArgumentException($"Unsupported HTTP method '{method}' has been set for route '{routeId}'."));
            }
        }
    }

    private static void ValidateHeaders(List<Exception> errors, IReadOnlyList<RouteHeader>? headers, string routeId)
    {
        // Headers are optional
        if (headers is null)
        {
            return;
        }

        foreach (var header in headers)
        {
            if (header is null)
            {
                errors.Add(new ArgumentException($"A null route header has been set for route '{routeId}'."));
                continue;
            }

            if (string.IsNullOrEmpty(header.Name))
            {
                errors.Add(new ArgumentException($"A null or empty route header name has been set for route '{routeId}'."));
            }

            if (header.Mode != HeaderMatchMode.Exists
                && (header.Values is null || header.Values.Count == 0))
            {
                errors.Add(new ArgumentException($"No header values were set on route header '{header.Name}' for route '{routeId}'."));
            }

            if (header.Mode == HeaderMatchMode.Exists && header.Values?.Count > 0)
            {
                errors.Add(new ArgumentException($"Header values where set when using mode '{nameof(HeaderMatchMode.Exists)}' on route header '{header.Name}' for route '{routeId}'."));
            }
        }
    }

    private static void ValidateQueryParameters(List<Exception> errors, IReadOnlyList<RouteQueryParameter>? queryparams, string routeId)
    {
        // Query Parameters are optional
        if (queryparams is null)
        {
            return;
        }

        foreach (var queryparam in queryparams)
        {
            if (queryparam is null)
            {
                errors.Add(new ArgumentException($"A null route query parameter has been set for route '{routeId}'."));
                continue;
            }

            if (string.IsNullOrEmpty(queryparam.Name))
            {
                errors.Add(new ArgumentException($"A null or empty route query parameter name has been set for route '{routeId}'."));
            }

            if (queryparam.Mode != QueryParameterMatchMode.Exists
                && (queryparam.Values is null || queryparam.Values.Count == 0))
            {
                errors.Add(new ArgumentException($"No query parameter values were set on route query parameter '{queryparam.Name}' for route '{routeId}'."));
            }

            if (queryparam.Mode == QueryParameterMatchMode.Exists && queryparam.Values?.Count > 0)
            {
                errors.Add(new ArgumentException($"Query parameter values where set when using mode '{nameof(QueryParameterMatchMode.Exists)}' on route query parameter '{queryparam.Name}' for route '{routeId}'."));
            }
        }
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

    private void ValidateLoadBalancing(IList<Exception> errors, ClusterConfig cluster)
    {
        var loadBalancingPolicy = cluster.LoadBalancingPolicy;
        if (string.IsNullOrEmpty(loadBalancingPolicy))
        {
            // The default.
            loadBalancingPolicy = LoadBalancingPolicies.PowerOfTwoChoices;
        }

        if (!_loadBalancingPolicies.ContainsKey(loadBalancingPolicy))
        {
            errors.Add(new ArgumentException($"No matching {nameof(ILoadBalancingPolicy)} found for the load balancing policy '{loadBalancingPolicy}' set on the cluster '{cluster.ClusterId}'."));
        }
    }

    private void ValidateSessionAffinity(IList<Exception> errors, ClusterConfig cluster)
    {
        if (!(cluster.SessionAffinity?.Enabled ?? false))
        {
            // Session affinity is disabled
            return;
        }

        // Note some affinity validation takes place in AffinitizeTransformProvider.ValidateCluster.

        var affinityFailurePolicy = cluster.SessionAffinity.FailurePolicy;
        if (string.IsNullOrEmpty(affinityFailurePolicy))
        {
            // The default.
            affinityFailurePolicy = SessionAffinityConstants.FailurePolicies.Redistribute;
        }

        if (!_affinityFailurePolicies.ContainsKey(affinityFailurePolicy))
        {
            errors.Add(new ArgumentException($"No matching {nameof(IAffinityFailurePolicy)} found for the affinity failure policy name '{affinityFailurePolicy}' set on the cluster '{cluster.ClusterId}'."));
        }

        if (string.IsNullOrEmpty(cluster.SessionAffinity.AffinityKeyName))
        {
            errors.Add(new ArgumentException($"Affinity key name set on the cluster '{cluster.ClusterId}' must not be null."));
        }

        var cookieConfig = cluster.SessionAffinity.Cookie;

        if (cookieConfig is null)
        {
            return;
        }

        if (cookieConfig.Expiration is not null && cookieConfig.Expiration <= TimeSpan.Zero)
        {
            errors.Add(new ArgumentException($"Session affinity cookie expiration must be positive or null."));
        }

        if (cookieConfig.MaxAge is not null && cookieConfig.MaxAge <= TimeSpan.Zero)
        {
            errors.Add(new ArgumentException($"Session affinity cookie max-age must be positive or null."));
        }
    }

    private static void ValidateProxyHttpClient(IList<Exception> errors, ClusterConfig cluster)
    {
        if (cluster.HttpClient is null)
        {
            // Proxy http client options are not set.
            return;
        }

        if (cluster.HttpClient.MaxConnectionsPerServer is not null && cluster.HttpClient.MaxConnectionsPerServer <= 0)
        {
            errors.Add(new ArgumentException($"Max connections per server limit set on the cluster '{cluster.ClusterId}' must be positive."));
        }
#if NET
        var encoding = cluster.HttpClient.RequestHeaderEncoding;
        if (encoding is not null)
        {
            try
            {
                Encoding.GetEncoding(encoding);
            }
            catch (ArgumentException aex)
            {
                errors.Add(new ArgumentException($"Invalid header encoding '{encoding}'.", aex));
            }
        }
#endif
    }

    private void ValidateProxyHttpRequest(IList<Exception> errors, ClusterConfig cluster)
    {
        if (cluster.HttpRequest is null)
        {
            // Proxy http request options are not set.
            return;
        }

        if (cluster.HttpRequest.Version is not null &&
            cluster.HttpRequest.Version != HttpVersion.Version10 &&
            cluster.HttpRequest.Version != HttpVersion.Version11 &&
            cluster.HttpRequest.Version != HttpVersion.Version20
#if NET6_0_OR_GREATER
            && cluster.HttpRequest.Version != HttpVersion.Version30
#endif
            )
        {
            errors.Add(new ArgumentException($"Outgoing request version '{cluster.HttpRequest.Version}' is not any of supported HTTP versions (1.0, 1.1, 2 and 3)."));
        }

        if (cluster.HttpRequest.Version == HttpVersion.Version10)
        {
            Log.ResponseReceived(_logger);
        }
    }

    private void ValidateHealthChecks(IList<Exception> errors, ClusterConfig cluster)
    {
        var availableDestinationsPolicy = cluster.HealthCheck?.AvailableDestinationsPolicy;
        if (string.IsNullOrEmpty(availableDestinationsPolicy))
        {
            // The default.
            availableDestinationsPolicy = HealthCheckConstants.AvailableDestinations.HealthyAndUnknown;
        }

        if (!_availableDestinationsPolicies.ContainsKey(availableDestinationsPolicy))
        {
            errors.Add(new ArgumentException($"No matching {nameof(IAvailableDestinationsPolicy)} found for the available destinations policy '{availableDestinationsPolicy}' set on the cluster.'{cluster.ClusterId}'."));
        }

        ValidateActiveHealthCheck(errors, cluster);
        ValidatePassiveHealthCheck(errors, cluster);
    }

    private void ValidateActiveHealthCheck(IList<Exception> errors, ClusterConfig cluster)
    {
        if (!(cluster.HealthCheck?.Active?.Enabled ?? false))
        {
            // Active health check is disabled
            return;
        }

        var activeOptions = cluster.HealthCheck.Active;
        var policy = activeOptions.Policy;
        if (string.IsNullOrEmpty(policy))
        {
            // default policy
            policy = HealthCheckConstants.ActivePolicy.ConsecutiveFailures;
        }
        if (!_activeHealthCheckPolicies.ContainsKey(policy))
        {
            errors.Add(new ArgumentException($"No matching {nameof(IActiveHealthCheckPolicy)} found for the active health check policy name '{policy}' set on the cluster '{cluster.ClusterId}'."));
        }

        if (activeOptions.Interval is not null && activeOptions.Interval <= TimeSpan.Zero)
        {
            errors.Add(new ArgumentException($"Destination probing interval set on the cluster '{cluster.ClusterId}' must be positive."));
        }

        if (activeOptions.Timeout is not null && activeOptions.Timeout <= TimeSpan.Zero)
        {
            errors.Add(new ArgumentException($"Destination probing timeout set on the cluster '{cluster.ClusterId}' must be positive."));
        }
    }

    private void ValidatePassiveHealthCheck(IList<Exception> errors, ClusterConfig cluster)
    {
        if (!(cluster.HealthCheck?.Passive?.Enabled ?? false))
        {
            // Passive health check is disabled
            return;
        }

        var passiveOptions = cluster.HealthCheck.Passive;
        var policy = passiveOptions.Policy;
        if (string.IsNullOrEmpty(policy))
        {
            // default policy
            policy = HealthCheckConstants.PassivePolicy.TransportFailureRate;
        }
        if (!_passiveHealthCheckPolicies.ContainsKey(policy))
        {
            errors.Add(new ArgumentException($"No matching {nameof(IPassiveHealthCheckPolicy)} found for the passive health check policy name '{policy}' set on the cluster '{cluster.ClusterId}'."));
        }

        if (passiveOptions.ReactivationPeriod is not null && passiveOptions.ReactivationPeriod <= TimeSpan.Zero)
        {
            errors.Add(new ArgumentException($"Unhealthy destination reactivation period set on the cluster '{cluster.ClusterId}' must be positive."));
        }
    }

    private static class Log
    {
        private static readonly Action<ILogger, Exception?> _requestVersionDetected = LoggerMessage.Define(
            LogLevel.Warning,
            EventIds.LoadData,
            "The HttpRequest version is set to 1.0 which can result in poor performance and port exhaustion. Use 1.1, 2, or 3 instead.");

        public static void Http10Version(ILogger logger)
        {
            _requestVersionDetected(logger, null);
        }
    }
}
