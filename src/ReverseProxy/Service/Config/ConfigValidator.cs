// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.ReverseProxy.Service.SessionAffinity;
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
        private static readonly Regex _hostNameRegex = new Regex(HostNameRegexPattern);

        private static readonly HashSet<string> _validMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HEAD", "OPTIONS", "GET", "PUT", "POST", "PATCH", "DELETE", "TRACE",
        };

        private readonly ITransformBuilder _transformBuilder;
        private readonly IAuthorizationPolicyProvider _authorizationPolicyProvider;
        private readonly ICorsPolicyProvider _corsPolicyProvider;
        private readonly IDictionary<string, ISessionAffinityProvider> _sessionAffinityProviders;
        private readonly IDictionary<string, IAffinityFailurePolicy> _affinityFailurePolicies;


        public ConfigValidator(ITransformBuilder transformBuilder,
            IAuthorizationPolicyProvider authorizationPolicyProvider,
            ICorsPolicyProvider corsPolicyProvider,
            IEnumerable<ISessionAffinityProvider> sessionAffinityProviders,
            IEnumerable<IAffinityFailurePolicy> affinityFailurePolicies)
        {
            _transformBuilder = transformBuilder ?? throw new ArgumentNullException(nameof(transformBuilder));
            _authorizationPolicyProvider = authorizationPolicyProvider ?? throw new ArgumentNullException(nameof(authorizationPolicyProvider));
            _corsPolicyProvider = corsPolicyProvider ?? throw new ArgumentNullException(nameof(corsPolicyProvider));
            _sessionAffinityProviders = sessionAffinityProviders?.ToProviderDictionary() ?? throw new ArgumentNullException(nameof(sessionAffinityProviders));
            _affinityFailurePolicies = affinityFailurePolicies?.ToPolicyDictionary() ?? throw new ArgumentNullException(nameof(affinityFailurePolicies));
        }

        // Note this performs all validation steps without short circuiting in order to report all possible errors.
        public async Task<IList<Exception>> ValidateRouteAsync(ProxyRoute route)
        {
            _ = route ?? throw new ArgumentNullException(nameof(route));
            var errors = new List<Exception>();

            if (string.IsNullOrEmpty(route.RouteId))
            {
                errors.Add(new ArgumentException("Missing Route Id."));
            }

            if ((route.Match.Hosts == null || route.Match.Hosts.Count == 0 || route.Match.Hosts.Any(host => string.IsNullOrEmpty(host))) && string.IsNullOrEmpty(route.Match.Path))
            {
                errors.Add(new ArgumentException($"Route `{route.RouteId}` requires Hosts or Path specified. Set the Path to `/{{**catchall}}` to match all requests."));
            }

            ValidateHost(errors, route.Match.Hosts, route.RouteId);
            ValidatePath(errors, route.Match.Path, route.RouteId);
            ValidateMethods(errors, route.Match.Methods, route.RouteId);
            errors.AddRange(_transformBuilder.Validate(route.Transforms));
            await ValidateAuthorizationPolicyAsync(errors, route.AuthorizationPolicy, route.RouteId);
            await ValidateCorsPolicyAsync(errors, route.CorsPolicy, route.RouteId);

            return errors;
        }

        // Note this performs all validation steps without short circuiting in order to report all possible errors.
        public Task<IList<Exception>> ValidateClusterAsync(Cluster cluster)
        {
            _ = cluster ?? throw new ArgumentNullException(nameof(cluster));
            var errors = new List<Exception>();

            if (string.IsNullOrEmpty(cluster.Id))
            {
                errors.Add(new ArgumentException("Missing Cluster Id."));
            }

            ValidateSessionAffinity(errors, cluster);

            return Task.FromResult<IList<Exception>>(errors);
        }

        private void ValidateHost(IList<Exception> errors, IReadOnlyList<string> hosts, string routeId)
        {
            // Host is optional when Path is specified
            if (hosts == null || hosts.Count == 0)
            {
                return;
            }

            for (var i = 0; i < hosts.Count; i++)
            {
                if (string.IsNullOrEmpty(hosts[i]) || !_hostNameRegex.IsMatch(hosts[i]))
                {
                    errors.Add(new ArgumentException($"Invalid host name '{hosts[i]}' for route `{routeId}`."));
                }
            }
        }

        private void ValidatePath(IList<Exception> errors, string path, string routeId)
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
                errors.Add(new ArgumentException($"Invalid path '{path}' for route `{routeId}`.", ex));
            }
        }

        private void ValidateMethods(IList<Exception> errors, IReadOnlyList<string> methods, string routeId)
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
                    errors.Add(new ArgumentException($"Duplicate http method '{method}' for route `{routeId}`."));
                    continue;
                }

                if (!_validMethods.Contains(method))
                {
                    errors.Add(new ArgumentException($"Unsupported Http method '{method}' has been set for route `{routeId}`."));
                }
            }
        }

        private async Task ValidateAuthorizationPolicyAsync(IList<Exception> errors, string authorizationPolicyName, string routeId)
        {
            if (string.IsNullOrEmpty(authorizationPolicyName))
            {
                return;
            }

            if (string.Equals(AuthorizationConstants.Default, authorizationPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var policy = await _authorizationPolicyProvider.GetPolicyAsync(authorizationPolicyName);
                if (policy == null)
                {
                    errors.Add(new ArgumentException($"Authorization policy '{authorizationPolicyName}' not found for route `{routeId}`."));
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ArgumentException($"Unable to retrieve the authorization policy '{authorizationPolicyName}' for route `{routeId}`.", ex));
            }
        }

        private async Task ValidateCorsPolicyAsync(IList<Exception> errors, string corsPolicyName, string routeId)
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
                    errors.Add(new ArgumentException($"CORS policy '{corsPolicyName}' not found for route `{routeId}`."));
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ArgumentException($"Unable to retrieve the CORS policy '{corsPolicyName}' for route `{routeId}`.", ex));
            }
        }

        private void ValidateSessionAffinity(IList<Exception> errors, Cluster cluster)
        {
            if (cluster.SessionAffinity == null || !cluster.SessionAffinity.Enabled)
            {
                // Session affinity is disabled
                return;
            }

            if (string.IsNullOrEmpty(cluster.SessionAffinity.Mode))
            {
                cluster.SessionAffinity.Mode = SessionAffinityConstants.Modes.Cookie;
            }

            var affinityMode = cluster.SessionAffinity.Mode;
            if (!_sessionAffinityProviders.ContainsKey(affinityMode))
            {
                errors.Add(new ArgumentException($"No matching ISessionAffinityProvider found for the session affinity mode `{affinityMode}` set on the cluster `{cluster.Id}`."));
            }

            if (string.IsNullOrEmpty(cluster.SessionAffinity.FailurePolicy))
            {
                cluster.SessionAffinity.FailurePolicy = SessionAffinityConstants.AffinityFailurePolicies.Redistribute;
            }

            var affinityFailurePolicy = cluster.SessionAffinity.FailurePolicy;
            if (!_affinityFailurePolicies.ContainsKey(affinityFailurePolicy))
            {
                errors.Add(new ArgumentException($"No matching IAffinityFailurePolicy found for the affinity failure policy name `{affinityFailurePolicy}` set on the cluster `{cluster.Id}`."));
            }
        }
    }
}
