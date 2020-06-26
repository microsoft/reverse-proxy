// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.RouteDiscovery.Contract;
using Microsoft.ReverseProxy.ConfigModel;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service
{
    internal class RouteValidator : IRouteValidator
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

        public RouteValidator(ITransformBuilder transformBuilder, IAuthorizationPolicyProvider authorizationPolicyProvider)
        {
            _transformBuilder = transformBuilder ?? throw new ArgumentNullException(nameof(transformBuilder));
            _authorizationPolicyProvider = authorizationPolicyProvider ?? throw new ArgumentNullException(nameof(authorizationPolicyProvider));
        }

        // Note this performs all validation steps without short circuiting in order to report all possible errors.
        public async Task<bool> ValidateRouteAsync(ParsedRoute route, IConfigErrorReporter errorReporter)
        {
            _ = route ?? throw new ArgumentNullException(nameof(route));
            _ = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));

            var success = true;
            if (string.IsNullOrEmpty(route.RouteId))
            {
                errorReporter.ReportError(ConfigErrors.ParsedRouteMissingId, route.RouteId, $"Route has no {nameof(route.RouteId)}.");
                success = false;
            }

            if (string.IsNullOrEmpty(route.Host) && string.IsNullOrEmpty(route.Path))
            {
                errorReporter.ReportError(ConfigErrors.ParsedRouteRuleHasNoMatchers, route.RouteId, $"Route requires {nameof(route.Host)} or {nameof(route.Path)} specified. Set the Path to `/{{**catchall}}` to match all requests.");
                success = false;
            }

            success &= ValidateHost(route.Host, route.RouteId, errorReporter);
            success &= ValidatePath(route.Path, route.RouteId, errorReporter);
            success &= ValidateMethods(route.Methods, route.RouteId, errorReporter);
            success &= _transformBuilder.Validate(route.Transforms, route.RouteId, errorReporter);
            success &= await ValidateAuthorizationPolicyAsync(route.AuthorizationPolicy, route.RouteId, errorReporter);

            return success;
        }

        private static bool ValidateHost(string host, string routeId, IConfigErrorReporter errorReporter)
        {
            // Host is optional when Path is specified
            if (string.IsNullOrEmpty(host))
            {
                return true;
            }

            if (!_hostNameRegex.IsMatch(host))
            {
                errorReporter.ReportError(ConfigErrors.ParsedRouteRuleInvalidMatcher, routeId, $"Invalid host name '{host}'");
                return false;
            }

            return true;
        }

        private static bool ValidatePath(string path, string routeId, IConfigErrorReporter errorReporter)
        {
            // Path is optional when Host is specified
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            try
            {
                RoutePatternFactory.Parse(path);
            }
            catch (RoutePatternException ex)
            {
                errorReporter.ReportError(ConfigErrors.ParsedRouteRuleInvalidMatcher, routeId, $"Invalid path pattern '{path}'", ex);
                return false;
            }

            return true;
        }

        private static bool ValidateMethods(IReadOnlyList<string> methods, string routeId, IConfigErrorReporter errorReporter)
        {
            // Methods are optional
            if (methods == null)
            {
                return true;
            }

            var seenMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var method in methods)
            {
                if (!seenMethods.Add(method))
                {
                    errorReporter.ReportError(ConfigErrors.ParsedRouteRuleInvalidMatcher, routeId, $"Duplicate verb '{method}'");
                    return false;
                }

                if (!_validMethods.Contains(method))
                {
                    errorReporter.ReportError(ConfigErrors.ParsedRouteRuleInvalidMatcher, routeId, $"Unsupported verb '{method}'");
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> ValidateAuthorizationPolicyAsync(string authorizationPolicyName, string routeId, IConfigErrorReporter errorReporter)
        {
            if (string.IsNullOrEmpty(authorizationPolicyName))
            {
                return true;
            }

            if (string.Equals(AuthorizationConstants.Default, authorizationPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                var policy = await _authorizationPolicyProvider.GetPolicyAsync(authorizationPolicyName);
                if (policy == null)
                {
                    errorReporter.ReportError(ConfigErrors.ParsedRouteRuleInvalidAuthorizationPolicy, routeId, $"Authorization policy '{authorizationPolicyName}' not found.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorReporter.ReportError(ConfigErrors.ParsedRouteRuleInvalidAuthorizationPolicy, routeId, $"Unable to retrieve the authorization policy '{authorizationPolicyName}'", ex);
                return false;
            }

            return true;
        }
    }
}
