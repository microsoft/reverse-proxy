// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// Helper class to parse configuration labels of the gateway into actual objects.
    /// </summary>
    // TODO: this is probably something that can be used in other integration modules apart from Service Fabric. Consider extracting to a general class.
    internal static class LabelsParser
    {
        // TODO: decide which labels are needed and which default table (and to what values)
        // Also probably move these defaults to the corresponding config entities.
        internal static readonly int? DefaultRouteOrder = null;

        private static readonly Regex _allowedRouteNamesRegex = new Regex("^[a-zA-Z0-9_-]+$");

        internal static TValue GetLabel<TValue>(Dictionary<string, string> labels, string key, TValue defaultValue)
        {
            if (!labels.TryGetValue(key, out var value))
            {
                return defaultValue;
            }
            else
            {
                try
                {
                    return (TValue)TypeDescriptor.GetConverter(typeof(TValue)).ConvertFromString(value);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is NotSupportedException)
                {
                    throw new ConfigException($"Could not convert label {key}='{value}' to type {typeof(TValue).FullName}.", ex);
                }
            }
        }

        // TODO: optimize this method
        internal static List<ProxyRoute> BuildRoutes(Uri serviceName, Dictionary<string, string> labels)
        {
            var backendId = GetClusterId(serviceName, labels);

            // Look for route IDs
            const string RoutesLabelsPrefix = "YARP.Routes.";
            var routesNames = new HashSet<string>();
            foreach (var kvp in labels)
            {
                if (kvp.Key.Length > RoutesLabelsPrefix.Length && kvp.Key.StartsWith(RoutesLabelsPrefix, StringComparison.Ordinal))
                {
                    var suffix = kvp.Key.Substring(RoutesLabelsPrefix.Length);
                    var routeNameLength = suffix.IndexOf('.');
                    if (routeNameLength == -1)
                    {
                        // No route name encoded, the key is not valid. Throwing would suggest we actually check for all invalid keys, so just ignore.
                        continue;
                    }
                    var routeName = suffix.Substring(0, routeNameLength);
                    if (!_allowedRouteNamesRegex.IsMatch(routeName))
                    {
                        throw new ConfigException($"Invalid route name '{routeName}', should only contain alphanumerical characters, underscores or hyphens.");
                    }
                    routesNames.Add(routeName);
                }
            }

            // Build the routes
            var routes = new List<ProxyRoute>();
            foreach (var routeName in routesNames)
            {
                var thisRoutePrefix = $"{RoutesLabelsPrefix}{routeName}";
                var metadata = new Dictionary<string, string>();
                foreach (var kvp in labels)
                {
                    if (kvp.Key.StartsWith($"{thisRoutePrefix}.Metadata.", StringComparison.Ordinal))
                    {
                        metadata.Add(kvp.Key.Substring($"{thisRoutePrefix}.Metadata.".Length), kvp.Value);
                    }
                }

                labels.TryGetValue($"{thisRoutePrefix}.Hosts", out var hosts);
                labels.TryGetValue($"{thisRoutePrefix}.Path", out var path);

                var route = new ProxyRoute
                {
                    RouteId = $"{Uri.EscapeDataString(backendId)}:{Uri.EscapeDataString(routeName)}",
                    Match =
                    {
                        Hosts = SplitHosts(hosts),
                        Path = path,
                    },
                    Order = GetLabel(labels, $"{thisRoutePrefix}.Order", DefaultRouteOrder),
                    ClusterId = backendId,
                    Metadata = metadata,
                };
                routes.Add(route);
            }
            return routes;
        }

        internal static Cluster BuildCluster(Uri serviceName, Dictionary<string, string> labels)
        {
            var clusterMetadata = new Dictionary<string, string>();
            Dictionary<string, string> sessionAffinitySettings = null;
            const string BackendMetadataKeyPrefix = "YARP.Backend.Metadata.";
            const string SessionAffinitySettingsKeyPrefix = "YARP.Backend.SessionAffinity.Settings.";
            foreach (var item in labels)
            {
                if (item.Key.StartsWith(BackendMetadataKeyPrefix, StringComparison.Ordinal))
                {
                    clusterMetadata[item.Key.Substring(BackendMetadataKeyPrefix.Length)] = item.Value;
                }
                else if (item.Key.StartsWith(SessionAffinitySettingsKeyPrefix, StringComparison.Ordinal))
                {
                    if (sessionAffinitySettings == null)
                    {
                        sessionAffinitySettings = new Dictionary<string, string>();
                    }

                    sessionAffinitySettings[item.Key.Substring(SessionAffinitySettingsKeyPrefix.Length)] = item.Value;
                }
            }

            var clusterId = GetClusterId(serviceName, labels);

            var cluster = new Cluster
            {
                Id = clusterId,
                LoadBalancing = Enum.TryParse(typeof(LoadBalancingMode), GetLabel<string>(labels, "YARP.Backend.LoadBalancing.Mode", null), out var parsed)
                    ? new LoadBalancingOptions { Mode = (LoadBalancingMode)parsed }
                    : null,
                SessionAffinity = new SessionAffinityOptions
                {
                    Enabled = GetLabel(labels, "YARP.Backend.SessionAffinity.Enabled", false),
                    Mode = GetLabel<string>(labels, "YARP.Backend.SessionAffinity.Mode", null),
                    FailurePolicy = GetLabel<string>(labels, "YARP.Backend.SessionAffinity.FailurePolicy", null),
                    Settings = sessionAffinitySettings
                },
                HttpRequest = new ProxyHttpRequestOptions
                {
                    Timeout = ToNullableTimeSpan(GetLabel<double?>(labels, "YARP.Backend.HttpRequest.Timeout", null)),
                    Version = Version.TryParse(GetLabel<string>(labels, "YARP.Backend.HttpRequest.Version", null), out var version) ? version : null,
#if NET
                    VersionPolicy = Enum.TryParse(typeof(HttpVersionPolicy), GetLabel<string>(labels, "YARP.Backend.HttpRequest.VersionPolicy", null), out var versionPolicy)
                        ? (HttpVersionPolicy?)versionPolicy
                        : null
#endif
                },
                HealthCheck = new HealthCheckOptions
                {
                    Active = new ActiveHealthCheckOptions
                    {
                        Enabled = GetLabel(labels, "YARP.Backend.HealthCheck.Active.Enabled", false),
                        Interval = ToNullableTimeSpan(GetLabel<double?>(labels, "YARP.Backend.HealthCheck.Active.Interval", null)),
                        Timeout = ToNullableTimeSpan(GetLabel<double?>(labels, "YARP.Backend.HealthCheck.Active.Timeout", null)),
                        Path = GetLabel<string>(labels, "YARP.Backend.HealthCheck.Active.Path", null),
                        Policy = GetLabel<string>(labels, "YARP.Backend.HealthCheck.Active.Policy", null)
                    },
                    Passive = new PassiveHealthCheckOptions
                    {
                        Enabled = GetLabel(labels, "YARP.Backend.HealthCheck.Passive.Enabled", false),
                        Policy = GetLabel<string>(labels, "YARP.Backend.HealthCheck.Passive.Policy", null),
                        ReactivationPeriod = ToNullableTimeSpan(GetLabel<double?>(labels, "YARP.Backend.HealthCheck.Passive.ReactivationPeriod", null))
                    }
                },
                Metadata = clusterMetadata,
            };
            return cluster;
        }

        private static TimeSpan? ToNullableTimeSpan(double? seconds)
        {
            return seconds.HasValue ? (TimeSpan?)TimeSpan.FromSeconds(seconds.Value) : null;
        }

        private static string GetClusterId(Uri serviceName, Dictionary<string, string> labels)
        {
            if (!labels.TryGetValue("YARP.Backend.BackendId", out var backendId) ||
                string.IsNullOrEmpty(backendId))
            {
                backendId = serviceName.ToString();
            }

            return backendId;
        }

        private static IReadOnlyList<string> SplitHosts(string hosts)
        {
            return hosts?.Split(',').Select(h => h.Trim()).Where(h => h.Length > 0).ToList();
        }
    }
}
