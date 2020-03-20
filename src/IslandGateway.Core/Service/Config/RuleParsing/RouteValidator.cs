// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;
using IslandGateway.Utilities;
using Microsoft.AspNetCore.Routing.Patterns;

namespace IslandGateway.Core.Service
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

        public bool ValidateRoute(ParsedRoute route, IConfigErrorReporter errorReporter)
        {
            Contracts.CheckValue(route, nameof(route));
            Contracts.CheckValue(errorReporter, nameof(errorReporter));

            var success = true;
            if (string.IsNullOrEmpty(route.RouteId))
            {
                errorReporter.ReportError(ConfigErrors.ParsedRouteMissingId, route.RouteId, $"Route has no {nameof(route.RouteId)}.");
                success = false;
            }

            if ((route.Matchers?.Count ?? 0) == 0)
            {
                errorReporter.ReportError(ConfigErrors.ParsedRouteRuleHasNoMatchers, route.RouteId, $"Route '{route.RouteId}' rule has no matchers.");
                success = false;
            }

            // TODO: Why is Host required? I'd only expect Host OR Path to be required, with Path being the more common usage.
            if (route.Matchers != null && !route.Matchers.Any(m => m is HostMatcher))
            {
                errorReporter.ReportError(ConfigErrors.ParsedRouteRuleMissingHostMatcher, route.RouteId, $"Route '{route.RouteId}' rule is missing required matcher 'Host()'.");
                success = false;
            }

            if (route.Matchers != null && !ValidateAllMatchers(route.RouteId, route.Matchers, errorReporter))
            {
                success = false;
            }

            return success;
        }

        private static bool ValidateAllMatchers(string routeId, IList<RuleMatcherBase> matchers, IConfigErrorReporter errorReporter)
        {
            var success = true;

            foreach (var matcher in matchers)
            {
                bool roundSuccess;
                string errorMessage;

                switch (matcher)
                {
                    case HostMatcher hostMatcher:
                        roundSuccess = ValidateHostMatcher(hostMatcher, out errorMessage);
                        break;
                    case PathMatcher pathMatcher:
                        roundSuccess = ValidatePathMatcher(pathMatcher, out errorMessage);
                        break;
                    case MethodMatcher methodMatcher:
                        roundSuccess = ValidateMethodMatcher(methodMatcher, out errorMessage);
                        break;
                    default:
                        roundSuccess = false;
                        errorMessage = "Unknown matcher";
                        break;
                }

                if (!roundSuccess)
                {
                    errorReporter.ReportError(ConfigErrors.ParsedRouteRuleInvalidMatcher, routeId, $"Invalid matcher '{matcher}'. {errorMessage}");
                    success = false;
                }
            }

            return success;
        }

        private static bool ValidateHostMatcher(HostMatcher hostMatcher, out string errorMessage)
        {
            if (!_hostNameRegex.IsMatch(hostMatcher.Host))
            {
                errorMessage = $"Invalid host name '{hostMatcher.Host}'";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool ValidatePathMatcher(PathMatcher pathMatcher, out string errorMessage)
        {
            try
            {
                RoutePatternFactory.Parse(pathMatcher.Pattern);
            }
            catch (RoutePatternException ex)
            {
                errorMessage = $"Invalid path pattern '{pathMatcher.Pattern}': {ex.Message}";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool ValidateMethodMatcher(MethodMatcher methodMatcher, out string errorMessage)
        {
            var seenMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var method in methodMatcher.Methods)
            {
                if (!seenMethods.Add(method))
                {
                    errorMessage = $"Duplicate verb '{method}'";
                    return false;
                }

                if (!_validMethods.Contains(method))
                {
                    errorMessage = $"Unsupported verb '{method}'";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
