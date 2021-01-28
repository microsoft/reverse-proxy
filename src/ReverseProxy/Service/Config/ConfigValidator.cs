// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.Abstractions.RouteDiscovery.Contract;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.HealthChecks;
using Microsoft.ReverseProxy.Service.LoadBalancing;
using Microsoft.ReverseProxy.Service.SessionAffinity;
using Microsoft.ReverseProxy.Utilities;
using CorsConstants = Microsoft.ReverseProxy.Abstractions.RouteDiscovery.Contract.CorsConstants;

namespace Microsoft.ReverseProxy.Service
{
    internal class ConfigValidator : IConfigValidator
    {
        // TODO: IDN support. How strictly do we need to validate this anyways? This is app config, not external input.
        /// <summary>
        /// Regex explanation:
        /// Either:
        ///    A) A simple label without dashes
        ///    B) A label containing dashes, but not as the first or last character.
        /// </summary>
        private const string DnsLabelRegexPattern = @"(?:[a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])";

        /// <summary>
        /// Regex explanation:
        ///    - Optionally, allow "*." in the beginning
        ///    - Then, one or more sequences of (LABEL ".")
        ///    - Then, one LABEL
        /// Where LABEL is described above in <see cref="DnsLabelRegexPattern"/>.
        /// </summary>
        private const string HostNameRegexPattern =
            @"^" +
            @"(?:\*\.)?" +
            @"(?:" + DnsLabelRegexPattern + @"\.)*" +
            DnsLabelRegexPattern +
            @"$";
        private static readonly Regex _hostNameRegex = new Regex(HostNameRegexPattern, RegexOptions.Compiled);

        private static readonly HashSet<string> _validMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HEAD", "OPTIONS", "GET", "PUT", "POST", "PATCH", "DELETE", "TRACE",
        };

        private readonly ITransformBuilder _transformBuilder;
        private readonly IAuthorizationPolicyProvider _authorizationPolicyProvider;
        private readonly ICorsPolicyProvider _corsPolicyProvider;
        private readonly IDictionary<string, ILoadBalancingPolicy> _loadBalancingPolicies;
        private readonly IDictionary<string, ISessionAffinityProvider> _sessionAffinityProviders;
        private readonly IDictionary<string, IAffinityFailurePolicy> _affinityFailurePolicies;
        private readonly IDictionary<string, IActiveHealthCheckPolicy> _activeHealthCheckPolicies;
        private readonly IDictionary<string, IPassiveHealthCheckPolicy> _passiveHealthCheckPolicies;


        public ConfigValidator(ITransformBuilder transformBuilder,
            IAuthorizationPolicyProvider authorizationPolicyProvider,
            ICorsPolicyProvider corsPolicyProvider,
            IEnumerable<ILoadBalancingPolicy> loadBalancingPolicies,
            IEnumerable<ISessionAffinityProvider> sessionAffinityProviders,
            IEnumerable<IAffinityFailurePolicy> affinityFailurePolicies,
            IEnumerable<IActiveHealthCheckPolicy> activeHealthCheckPolicies,
            IEnumerable<IPassiveHealthCheckPolicy> passiveHealthCheckPolicies)
        {
            _transformBuilder = transformBuilder ?? throw new ArgumentNullException(nameof(transformBuilder));
            _authorizationPolicyProvider = authorizationPolicyProvider ?? throw new ArgumentNullException(nameof(authorizationPolicyProvider));
            _corsPolicyProvider = corsPolicyProvider ?? throw new ArgumentNullException(nameof(corsPolicyProvider));
            _loadBalancingPolicies = loadBalancingPolicies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(loadBalancingPolicies));
            _sessionAffinityProviders = sessionAffinityProviders?.ToDictionaryByUniqueId(p => p.Mode) ?? throw new ArgumentNullException(nameof(sessionAffinityProviders));
            _affinityFailurePolicies = affinityFailurePolicies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(affinityFailurePolicies));
            _activeHealthCheckPolicies = activeHealthCheckPolicies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(activeHealthCheckPolicies));
            _passiveHealthCheckPolicies = passiveHealthCheckPolicies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(passiveHealthCheckPolicies));
        }

        // Note this performs all validation steps without short circuiting in order to report all possible errors.
        public async ValueTask<IList<Exception>> ValidateRouteAsync(ProxyRoute route)
        {
            _ = route ?? throw new ArgumentNullException(nameof(route));
            var errors = new List<Exception>();

            if (string.IsNullOrEmpty(route.RouteId))
            {
                errors.Add(new ArgumentException("Missing Route Id."));
            }

            errors.AddRange(_transformBuilder.Validate(route.Transforms));
            await ValidateAuthorizationPolicyAsync(errors, route.AuthorizationPolicy, route.RouteId);
            await ValidateCorsPolicyAsync(errors, route.CorsPolicy, route.RouteId);

            if (route.Match == null)
            {
                errors.Add(new ArgumentException($"Route '{route.RouteId}' did not set any match criteria, it requires Hosts or Path specified. Set the Path to '/{{**catchall}}' to match all requests."));
                return errors;
            }

            if ((route.Match.Hosts == null || route.Match.Hosts.Count == 0 || route.Match.Hosts.Any(host => string.IsNullOrEmpty(host))) && string.IsNullOrEmpty(route.Match.Path))
            {
                errors.Add(new ArgumentException($"Route '{route.RouteId}' requires Hosts or Path specified. Set the Path to '/{{**catchall}}' to match all requests."));
            }

            ValidateHost(errors, route.Match.Hosts, route.RouteId);
            ValidatePath(errors, route.Match.Path, route.RouteId);
            ValidateMethods(errors, route.Match.Methods, route.RouteId);
            ValidateHeaders(errors, route.Match.Headers, route.RouteId);

            return errors;
        }

        // Note this performs all validation steps without short circuiting in order to report all possible errors.
        public ValueTask<IList<Exception>> ValidateClusterAsync(Cluster cluster)
        {
            _ = cluster ?? throw new ArgumentNullException(nameof(cluster));
            var errors = new List<Exception>();

            if (string.IsNullOrEmpty(cluster.Id))
            {
                errors.Add(new ArgumentException("Missing Cluster Id."));
            }

            ValidateLoadBalancing(errors, cluster);
            ValidateSessionAffinity(errors, cluster);
            ValidateProxyHttpClient(errors, cluster);
            ValidateProxyHttpRequest(errors, cluster);
            ValidateActiveHealthCheck(errors, cluster);
            ValidatePassiveHealthCheck(errors, cluster);

            return new ValueTask<IList<Exception>>(errors);
        }

        private static void ValidateHost(IList<Exception> errors, IReadOnlyList<string> hosts, string routeId)
        {
            // Host is optional when Path is specified
            if (hosts == null || hosts.Count == 0)
            {
                return;
            }

            foreach (var host in hosts)
            {
                if (string.IsNullOrEmpty(host) || !_hostNameRegex.IsMatch(host))
                {
                    errors.Add(new ArgumentException($"Invalid host name '{host}' for route '{routeId}'."));
                }
            }
        }

        private static void ValidatePath(IList<Exception> errors, string path, string routeId)
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

        private static void ValidateMethods(IList<Exception> errors, IReadOnlyList<string> methods, string routeId)
        {
            // Methods are optional
            if (methods == null)
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

        private static void ValidateHeaders(List<Exception> errors, IReadOnlyList<RouteHeader> headers, string routeId)
        {
            // Headers are optional
            if (headers == null)
            {
                return;
            }

            foreach (var header in headers)
            {
                if (header == null)
                {
                    errors.Add(new ArgumentException($"A null route header has been set for route '{routeId}'."));
                    continue;
                }

                if (string.IsNullOrEmpty(header.Name))
                {
                    errors.Add(new ArgumentException($"A null or empty route header name has been set for route '{routeId}'."));
                }

                if (header.Mode != HeaderMatchMode.Exists
                    && (header.Values == null || header.Values.Count == 0))
                {
                    errors.Add(new ArgumentException($"No header values were set on route header '{header.Name}' for route '{routeId}'."));
                }

                if (header.Mode == HeaderMatchMode.Exists && header.Values?.Count > 0)
                {
                    errors.Add(new ArgumentException($"Header values where set when using mode '{nameof(HeaderMatchMode.Exists)}' on route header '{header.Name}' for route '{routeId}'."));
                }
            }
        }

        private async ValueTask ValidateAuthorizationPolicyAsync(IList<Exception> errors, string authorizationPolicyName, string routeId)
        {
            if (string.IsNullOrEmpty(authorizationPolicyName))
            {
                return;
            }

            if (string.Equals(AuthorizationConstants.Default, authorizationPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(AuthorizationConstants.Anonymous, authorizationPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var policy = await _authorizationPolicyProvider.GetPolicyAsync(authorizationPolicyName);
                if (policy == null)
                {
                    errors.Add(new ArgumentException($"Authorization policy '{authorizationPolicyName}' not found for route '{routeId}'."));
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ArgumentException($"Unable to retrieve the authorization policy '{authorizationPolicyName}' for route '{routeId}'.", ex));
            }
        }

        private async ValueTask ValidateCorsPolicyAsync(IList<Exception> errors, string corsPolicyName, string routeId)
        {
            if (string.IsNullOrEmpty(corsPolicyName))
            {
                return;
            }

            if (string.Equals(CorsConstants.Default, corsPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(CorsConstants.Disable, corsPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var dummyHttpContext = new DefaultHttpContext();
                var policy = await _corsPolicyProvider.GetPolicyAsync(dummyHttpContext, corsPolicyName);
                if (policy == null)
                {
                    errors.Add(new ArgumentException($"CORS policy '{corsPolicyName}' not found for route '{routeId}'."));
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ArgumentException($"Unable to retrieve the CORS policy '{corsPolicyName}' for route '{routeId}'.", ex));
            }
        }

        private void ValidateLoadBalancing(IList<Exception> errors, Cluster cluster)
        {
            var loadBalancingPolicy = cluster.LoadBalancingPolicy;
            if (string.IsNullOrEmpty(loadBalancingPolicy))
            {
                // The default.
                loadBalancingPolicy = LoadBalancingPolicies.PowerOfTwoChoices;
            }

            if (!_loadBalancingPolicies.ContainsKey(loadBalancingPolicy))
            {
                errors.Add(new ArgumentException($"No matching {nameof(ILoadBalancingPolicy)} found for the load balancing policy '{loadBalancingPolicy}' set on the cluster '{cluster.Id}'."));
            }
        }

        private void ValidateSessionAffinity(IList<Exception> errors, Cluster cluster)
        {
            if (!(cluster.SessionAffinity?.Enabled ?? false))
            {
                // Session affinity is disabled
                return;
            }

            var affinityMode = cluster.SessionAffinity.Mode;
            if (string.IsNullOrEmpty(affinityMode))
            {
                // The default.
                affinityMode = SessionAffinityConstants.Modes.Cookie;
            }

            if (!_sessionAffinityProviders.ContainsKey(affinityMode))
            {
                errors.Add(new ArgumentException($"No matching {nameof(ISessionAffinityProvider)} found for the session affinity mode '{affinityMode}' set on the cluster '{cluster.Id}'."));
            }

            var affinityFailurePolicy = cluster.SessionAffinity.FailurePolicy;
            if (string.IsNullOrEmpty(affinityFailurePolicy))
            {
                // The default.
                affinityFailurePolicy = SessionAffinityConstants.AffinityFailurePolicies.Redistribute;
            }

            if (!_affinityFailurePolicies.ContainsKey(affinityFailurePolicy))
            {
                errors.Add(new ArgumentException($"No matching IAffinityFailurePolicy found for the affinity failure policy name '{affinityFailurePolicy}' set on the cluster '{cluster.Id}'."));
            }
        }

        private static void ValidateProxyHttpClient(IList<Exception> errors, Cluster cluster)
        {
            if (cluster.HttpClient == null)
            {
                // Proxy http client options are not set.
                return;
            }

            if (cluster.HttpClient.MaxConnectionsPerServer != null && cluster.HttpClient.MaxConnectionsPerServer <= 0)
            {
                errors.Add(new ArgumentException($"Max connections per server limit set on the cluster '{cluster.Id}' must be positive."));
            }
        }

        private static void ValidateProxyHttpRequest(IList<Exception> errors, Cluster cluster)
        {
            if (cluster.HttpRequest == null)
            {
                // Proxy http request options are not set.
                return;
            }

            if (cluster.HttpRequest.Version != null &&
                cluster.HttpRequest.Version != HttpVersion.Version10 &&
                cluster.HttpRequest.Version != HttpVersion.Version11 &&
                cluster.HttpRequest.Version != HttpVersion.Version20)
            {
                errors.Add(new ArgumentException($"Outgoing request version '{cluster.HttpRequest.Version}' is not any of supported HTTP versions (1.0, 1.1 and 2)."));
            }
        }

        private void ValidateActiveHealthCheck(IList<Exception> errors, Cluster cluster)
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
                errors.Add(new ArgumentException($"Active health policy name is not set on the cluster '{cluster.Id}'"));
            }
            else if (!_activeHealthCheckPolicies.ContainsKey(policy))
            {
                errors.Add(new ArgumentException($"No matching {nameof(IActiveHealthCheckPolicy)} found for the active health check policy name '{policy}' set on the cluster '{cluster.Id}'."));
            }

            if (activeOptions.Interval != null && activeOptions.Interval <= TimeSpan.Zero)
            {
                errors.Add(new ArgumentException($"Destination probing interval set on the cluster '{cluster.Id}' must be positive."));
            }

            if (activeOptions.Timeout != null && activeOptions.Timeout <= TimeSpan.Zero)
            {
                errors.Add(new ArgumentException($"Destination probing timeout set on the cluster '{cluster.Id}' must be positive."));
            }
        }

        private static void ValidatePassiveHealthCheck(IList<Exception> errors, Cluster cluster)
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
                errors.Add(new ArgumentException($"Passive health policy name is not set on the cluster '{cluster.Id}'"));
            }

            if (passiveOptions.ReactivationPeriod != null && passiveOptions.ReactivationPeriod <= TimeSpan.Zero)
            {
                errors.Add(new ArgumentException($"Unhealthy destination reactivation period set on the cluster '{cluster.Id}' must be positive."));
            }
        }
    }
}
