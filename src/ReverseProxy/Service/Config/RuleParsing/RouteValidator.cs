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
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.RouteDiscovery.Contract;
using Microsoft.ReverseProxy.Service.Config;
using CorsConstants = Microsoft.ReverseProxy.Abstractions.RouteDiscovery.Contract.CorsConstants;

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
        private readonly ICorsPolicyProvider _corsPolicyProvider;
        private readonly ILogger<RouteValidator> _logger;

        public RouteValidator(ITransformBuilder transformBuilder, IAuthorizationPolicyProvider authorizationPolicyProvider, ICorsPolicyProvider corsPolicyProvider, ILogger<RouteValidator> logger)
        {
            _transformBuilder = transformBuilder ?? throw new ArgumentNullException(nameof(transformBuilder));
            _authorizationPolicyProvider = authorizationPolicyProvider ?? throw new ArgumentNullException(nameof(authorizationPolicyProvider));
            _corsPolicyProvider = corsPolicyProvider ?? throw new ArgumentNullException(nameof(corsPolicyProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Note this performs all validation steps without short circuiting in order to report all possible errors.
        public async Task<bool> ValidateRouteAsync(ProxyRoute route)
        {
            _ = route ?? throw new ArgumentNullException(nameof(route));

            var success = true;
            if (string.IsNullOrEmpty(route.RouteId))
            {
                Log.MissingRouteId(_logger);
                success = false;
            }

            if ((route.Match.Hosts == null || route.Match.Hosts.Count == 0 || route.Match.Hosts.Any(host => string.IsNullOrEmpty(host))) && string.IsNullOrEmpty(route.Match.Path))
            {
                Log.MissingRouteMatchers(_logger, route.RouteId);
                success = false;
            }

            success &= ValidateHost(route.Match.Hosts, route.RouteId);
            success &= ValidatePath(route.Match.Path, route.RouteId);
            success &= ValidateMethods(route.Match.Methods, route.RouteId);
            success &= _transformBuilder.Validate(route.Transforms, route.RouteId);
            success &= await ValidateAuthorizationPolicyAsync(route.AuthorizationPolicy, route.RouteId);
            success &= await ValidateCorsPolicyAsync(route.CorsPolicy, route.RouteId);

            return success;
        }

        private bool ValidateHost(IReadOnlyList<string> hosts, string routeId)
        {
            // Host is optional when Path is specified
            if (hosts == null || hosts.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < hosts.Count; i++)
            {
                if (string.IsNullOrEmpty(hosts[i]) || !_hostNameRegex.IsMatch(hosts[i]))
                {
                    Log.InvalidRouteHost(_logger, hosts[i], routeId);
                    return false;
                }
            }

            return true;
        }

        private bool ValidatePath(string path, string routeId)
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
                Log.InvalidRoutePath(_logger, path, routeId, ex);
                return false;
            }

            return true;
        }

        private bool ValidateMethods(IReadOnlyList<string> methods, string routeId)
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
                    Log.DuplicateHttpMethod(_logger, method, routeId);
                    return false;
                }

                if (!_validMethods.Contains(method))
                {
                    Log.UnsupportedHttpMethod(_logger, method, routeId);
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> ValidateAuthorizationPolicyAsync(string authorizationPolicyName, string routeId)
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
                    Log.AuthorizationPolicyNotFound(_logger, authorizationPolicyName, routeId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.FailedRetrieveAuthorizationPolicy(_logger, authorizationPolicyName, routeId, ex);
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateCorsPolicyAsync(string corsPolicyName, string routeId)
        {
            if (string.IsNullOrEmpty(corsPolicyName))
            {
                return true;
            }

            if (string.Equals(CorsConstants.Default, corsPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(CorsConstants.Disable, corsPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                var dummyHttpContext = new DefaultHttpContext();
                var policy = await _corsPolicyProvider.GetPolicyAsync(dummyHttpContext, corsPolicyName);
                if (policy == null)
                {
                    Log.CorsPolicyNotFound(_logger, corsPolicyName, routeId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.FailedRetrieveCorsPolicy(_logger, corsPolicyName, routeId, ex);
                return false;
            }

            return true;
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception> _missingRouteId = LoggerMessage.Define(
                LogLevel.Error,
                EventIds.MissingRouteId,
                "Route has no RouteId.");

            private static readonly Action<ILogger, string, Exception> _missingRouteMatchers = LoggerMessage.Define<string>(
                LogLevel.Error,
                EventIds.MissingRouteMatchers,
                "Route `{routeId}` requires Hosts or Path specified. Set the Path to `/{{**catchall}}` to match all requests.");

            private static readonly Action<ILogger, string, string, Exception> _invalidRouteHost = LoggerMessage.Define<string, string>(
                LogLevel.Error,
                EventIds.InvalidRouteHost,
                "Invalid host name '{host}' for route `{routeId}`.");

            private static readonly Action<ILogger, string, string, Exception> _invalidRoutePath = LoggerMessage.Define<string, string>(
                LogLevel.Error,
                EventIds.InvalidRoutePath,
                "Invalid path '{path}' for route `{routeId}`.");

            private static readonly Action<ILogger, string, string, Exception> _duplicateHttpMethod = LoggerMessage.Define<string, string>(
                LogLevel.Error,
                EventIds.DuplicateHttpMethod,
                "Duplicate http method '{method}' for route `{routeId}`.");

            private static readonly Action<ILogger, string, string, Exception> _unsupportedHttpMethod = LoggerMessage.Define<string, string>(
                LogLevel.Error,
                EventIds.UnsupportedHttpMethod,
                "Unsupported Http method '{method}' has been set for route `{routeId}`.");

            private static readonly Action<ILogger, string, string, Exception> _authorizationPolicyNotFound = LoggerMessage.Define<string, string>(
                LogLevel.Error,
                EventIds.AuthorizationPolicyNotFound,
                "Authorization policy '{authorizationPolicy}' not found for route `{routeId}`.");

            private static readonly Action<ILogger, string, string, Exception> _failedRetrieveAuthorizationPolicy = LoggerMessage.Define<string, string>(
                LogLevel.Error,
                EventIds.FailedRetrieveAuthorizationPolicy,
                "Unable to retrieve the authorization policy '{authorizationPolicy}' for route `{routeId}`.");

            private static readonly Action<ILogger, string, string, Exception> _corsPolicyNotFound = LoggerMessage.Define<string, string>(
                LogLevel.Error,
                EventIds.CorsPolicyNotFound,
                "CORS policy '{corsPolicy}' not found for route `{routeId}`.");

            private static readonly Action<ILogger, string, string, Exception> _failedRetrieveCorsPolicy = LoggerMessage.Define<string, string>(             LogLevel.Error,
                EventIds.FailedRetrieveCorsPolicy,
                "Unable to retrieve the CORS policy '{corsPolicy}' for route `{routeId}`.");

            public static void MissingRouteId(ILogger logger)
            {
                _missingRouteId(logger, null);
            }

            public static void MissingRouteMatchers(ILogger logger, string routeId)
            {
                _missingRouteMatchers(logger, routeId, null);
            }

            public static void InvalidRouteHost(ILogger logger, string host, string routeId)
            {
                _invalidRouteHost(logger, host, routeId, null);
            }

            public static void InvalidRoutePath(ILogger logger, string path, string routeId, Exception exception)
            {
                _invalidRoutePath(logger, path, routeId, exception);
            }

            public static void DuplicateHttpMethod(ILogger logger, string method, string routeId)
            {
                _duplicateHttpMethod(logger, method, routeId, null);
            }

            public static void UnsupportedHttpMethod(ILogger logger, string method, string routeId)
            {
                _unsupportedHttpMethod(logger, method, routeId, null);
            }

            public static void AuthorizationPolicyNotFound(ILogger logger, string authorizationPolicy, string routeId)
            {
                _authorizationPolicyNotFound(logger, authorizationPolicy, routeId, null);
            }

            public static void FailedRetrieveAuthorizationPolicy(ILogger logger, string authorizationPolicy, string routeId, Exception exception)
            {
                _failedRetrieveAuthorizationPolicy(logger, authorizationPolicy, routeId, exception);
            }

            public static void CorsPolicyNotFound(ILogger logger, string corsPolicy, string routeId)
            {
                _corsPolicyNotFound(logger, corsPolicy, routeId, null);
            }

            public static void FailedRetrieveCorsPolicy(ILogger logger, string corsPolicy, string routeId, Exception exception)
            {
                _failedRetrieveCorsPolicy(logger, corsPolicy, routeId, exception);
            }
        }
    }
}
