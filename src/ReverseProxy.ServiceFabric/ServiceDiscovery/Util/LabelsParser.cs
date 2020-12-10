// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Utilities;
using Microsoft.ReverseProxy.ServiceFabric.Utilities;

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

        /// <summary>
        /// Requires all header match names to follow the .[0]. pattern to simulate indexing in an array
        /// </summary>
        private static readonly Regex _allowedHeaderNamesRegex = new Regex(@"^\[\d\d*\]$");


        /// Requires all transform names to follow the .[0]. pattern to simulate indexing in an array
        /// </summary>
        private static readonly Regex _allowedTransformNamesRegex = new Regex(@"^\[\d\d*\]$");

        // Look for route IDs
        private const string RoutesLabelsPrefix = "YARP.Routes.";

        internal static TValue GetLabel<TValue>(Dictionary<string, string> labels, string key, TValue defaultValue)
        {
            if (!labels.TryGetValue(key, out var value))
            {
                return defaultValue;
            }
            else
            {
                return ConvertLabelValue<TValue>(key, value);
            }
        }

        private static TValue ConvertLabelValue<TValue>(string key, string value)
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

        // TODO: optimize this method
        internal static List<ProxyRoute> BuildRoutes(Uri serviceName, Dictionary<string, string> labels)
        {
            var backendId = GetClusterId(serviceName, labels);

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
                string hosts = null;
                string path = null;
                int? order = null;
                var metadata = new Dictionary<string, string>();
                var headerMatches = new Dictionary<string, RouteHeader>();
                var transforms = new Dictionary<string, IDictionary<string, string>>();
                foreach (var kvp in labels)
                {
                    if (ContainsKey(routeName, "Metadata.", kvp.Key, out var keyNameEnd))
                    {
                        metadata.Add(kvp.Key.Substring(keyNameEnd), kvp.Value);
                    }
                    else if (kvp.Key.StartsWith($"{thisRoutePrefix}.MatchHeaders.", StringComparison.Ordinal)) 
                    {
                        var suffix = kvp.Key.Substring($"{thisRoutePrefix}.MatchHeaders.".Length);
                        var headerIndexLength = suffix.IndexOf('.');
                        if (headerIndexLength == -1)
                        {
                            // No header encoded, the key is not valid. Throwing would suggest we actually check for all invalid keys, so just ignore.
                            continue;
                        }
                        var headerIndex = suffix.Substring(0, headerIndexLength);
                        if (!_allowedHeaderNamesRegex.IsMatch(headerIndex))
                        {
                            throw new ConfigException($"Invalid header matching index '{headerIndex}', should only contain alphanumerical characters, underscores or hyphens.");
                        }
                        if (!headerMatches.ContainsKey(headerIndex)) 
                        {
                            headerMatches.Add(headerIndex, new RouteHeader());
                        }

                        var propertyName = kvp.Key.Substring($"{thisRoutePrefix}.MatchHeaders.{headerIndex}.".Length);
                        if (propertyName.Equals("Name", StringComparison.Ordinal)) 
                        {
                            headerMatches[headerIndex].Name = kvp.Value;
                        } 
                        else if (propertyName.Equals("Values", StringComparison.Ordinal)) 
                        {
#if NET5_0
                            headerMatches[headerIndex].Values = kvp.Value.Split(',', StringSplitOptions.TrimEntries);
#elif NETCOREAPP3_1
                            headerMatches[headerIndex].Values = kvp.Value.Split(',').Select(val => val.Trim()).ToList();
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
                        }
                        else if (propertyName.Equals("IsCaseSensitive", StringComparison.Ordinal)) 
                        {
                            headerMatches[headerIndex].IsCaseSensitive = bool.Parse(kvp.Value);
                        }
                        else if (propertyName.Equals("Mode", StringComparison.Ordinal)) 
                        {
                            headerMatches[headerIndex].Mode = Enum.Parse<HeaderMatchMode>(kvp.Value);
                        }
                        else
                        {
                            throw new ConfigException($"Invalid header matching property '{propertyName}', only valid values are Name, Values, IsCaseSensitive and Mode.");
                        }
                    }
                    else if (ContainsKey(routeName, "Transforms.", kvp.Key, out keyNameEnd))
                    {
                        var suffix = kvp.Key.Substring(keyNameEnd);
                        var transformNameLength = suffix.IndexOf('.');
                        if (transformNameLength == -1)
                        {
                            // No transform index encoded, the key is not valid. Throwing would suggest we actually check for all invalid keys, so just ignore.
                            continue;
                        }
                        var transformName = suffix.Substring(0, transformNameLength);
                        if (!_allowedTransformNamesRegex.IsMatch(transformName))
                        {
                            throw new ConfigException($"Invalid transform index '{transformName}', should be transform index wrapped in square brackets.");
                        }
                        if (!transforms.ContainsKey(transformName))
                        {
                            transforms.Add(transformName, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                        }
                        var propertyName = kvp.Key.Substring(keyNameEnd + transformNameLength + 1);
                        if (!transforms[transformName].ContainsKey(propertyName))
                        {
                            transforms[transformName].Add(propertyName, kvp.Value);
                        }
                        else
                        {
                            throw new ConfigException($"A duplicate transformation property '{transformName}.{propertyName}' was found.");
                        }
                    }
                    else if (ContainsKey(routeName, "Hosts", kvp.Key, out keyNameEnd))
                    {
                        hosts = kvp.Value;
                    }
                    else if (ContainsKey(routeName, "Path", kvp.Key, out keyNameEnd))
                    {
                        path = kvp.Value;
                    }
                    else if (ContainsKey(routeName, "Order", kvp.Key, out keyNameEnd))
                    {
                        order = ConvertLabelValue<int?>(kvp.Key, kvp.Value);
                    }
                }

                var route = new ProxyRoute
                {
                    RouteId = $"{Uri.EscapeDataString(backendId)}:{Uri.EscapeDataString(routeName)}",
                    Match =
                    {
                        Hosts = SplitHosts(hosts),
                        Path = path,
                        Headers = headerMatches.Count > 0 ? headerMatches.Select(hm => hm.Value).ToArray() : null
                    },
                    Order = order ?? DefaultRouteOrder,
                    ClusterId = backendId,
                    Metadata = metadata,
                    Transforms = transforms.Count > 0 ? transforms.Select(tr => tr.Value).ToList() : null
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

            var loadBalancingModeLabel = GetLabel<string>(labels, "YARP.Backend.LoadBalancing.Mode", null);
            var versionLabel = GetLabel<string>(labels, "YARP.Backend.HttpRequest.Version", null);
#if NET
            var versionPolicyLabel = GetLabel<string>(labels, "YARP.Backend.HttpRequest.VersionPolicy", null);
#endif
            var cluster = new Cluster
            {
                Id = clusterId,
                LoadBalancing = !string.IsNullOrEmpty(loadBalancingModeLabel)
                    ? new LoadBalancingOptions { Mode = (LoadBalancingMode)Enum.Parse(typeof(LoadBalancingMode), loadBalancingModeLabel) }
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
                    Version = !string.IsNullOrEmpty(versionLabel) ? Version.Parse(versionLabel + (versionLabel.Contains('.') ? "" : ".0")) : null,
#if NET
                    VersionPolicy = !string.IsNullOrEmpty(versionLabel) ? (HttpVersionPolicy)Enum.Parse(typeof(HttpVersionPolicy), versionPolicyLabel) : null
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

        private static bool ContainsKey(string routeName, string expectedKeyName, string actualKey, out int keyNameEnd)
        {
            keyNameEnd = -1;
            var prefixStart = actualKey.IndexOf(RoutesLabelsPrefix, StringComparison.Ordinal);
            if (prefixStart < 0)
            {
                return false;
            }

            var routeNameStart = actualKey.IndexOf(routeName, prefixStart + RoutesLabelsPrefix.Length, StringComparison.Ordinal);
            if (routeNameStart < 0)
            {
                return false;
            }

            var routeNameEnd = routeNameStart + routeName.Length;
            if (actualKey.Length == routeNameEnd || actualKey[routeNameEnd] != '.')
            {
                return false;
            }

            var keyNameStart = actualKey.IndexOf(expectedKeyName, routeNameEnd, StringComparison.Ordinal);
            if (keyNameStart < 0)
            {
                return false;
            }

            keyNameEnd = keyNameStart + expectedKeyName.Length;

            return true;
        }
    }
}
