using System;
using System.Collections.Generic;
using System.Linq;
using k8s.Models;
using YamlDotNet.Serialization;
using Yarp.ReverseProxy.Configuration;
using Yarp.Kubernetes.Controller.Caching;

namespace Yarp.Kubernetes.Controller.Converters;

internal static class YarpParser
{
    private static readonly Deserializer YamlDeserializer = new();

    internal static void ConvertFromKubernetesIngress(YarpIngressContext ingressContext, YarpConfigContext configContext)
    {
        var spec = ingressContext.Ingress.Spec;
        var defaultBackend = spec?.DefaultBackend;
        var defaultService = defaultBackend?.Service;
        IList<V1EndpointSubset> defaultSubsets = default;

        if (!string.IsNullOrEmpty(defaultService?.Name))
        {
            defaultSubsets = ingressContext.Endpoints.SingleOrDefault(x => x.Name == defaultService?.Name).Subsets;
        }

        // cluster can contain multiple replicas for each destination, need to know the lookup base don endpoints
        var options = HandleAnnotations(ingressContext, ingressContext.Ingress.Metadata);

        foreach (var rule in spec?.Rules ?? Enumerable.Empty<V1IngressRule>())
        {
            HandleIngressRule(ingressContext, ingressContext.Endpoints, defaultSubsets, rule, configContext);
        }
    }

    private static void HandleIngressRule(YarpIngressContext ingressContext, List<Endpoints> endpoints, IList<V1EndpointSubset> defaultSubsets, V1IngressRule rule, YarpConfigContext configContext)
    {
        var http = rule.Http;
        foreach (var path in http.Paths ?? Enumerable.Empty<V1HTTPIngressPath>())
        {
            var service = ingressContext.Services.SingleOrDefault(s => s.Metadata.Name == path.Backend.Service.Name);
            var servicePort = service.Spec.Ports.SingleOrDefault(p => MatchesPort(p, path.Backend.Service.Port));
            HandleIngressRulePath(ingressContext, servicePort, endpoints, defaultSubsets, rule, path, configContext);
        }
    }

    private static void HandleIngressRulePath(YarpIngressContext ingressContext, V1ServicePort servicePort, List<Endpoints> endpoints, IList<V1EndpointSubset> defaultSubsets, V1IngressRule rule, V1HTTPIngressPath path, YarpConfigContext configContext)
    {
        var backend = path.Backend;
        var ingressServiceBackend = backend.Service;
        var subsets = defaultSubsets;

        var clusters = configContext.ClusterTransfers;
        var routes = configContext.Routes;

        if (!string.IsNullOrEmpty(ingressServiceBackend?.Name))
        {
            subsets = endpoints.SingleOrDefault(x => x.Name == ingressServiceBackend?.Name).Subsets;
        }

        // Each ingress rule path can only be for one service
        var key = UpstreamName(ingressContext.Ingress.Metadata.NamespaceProperty, ingressServiceBackend);
        if (!clusters.ContainsKey(key))
        {
            clusters.Add(key, new ClusterTransfer());
        }

        var cluster = clusters[key];
        cluster.ClusterId = key;
        cluster.LoadBalancingPolicy = ingressContext.Options.LoadBalancingPolicy;
        cluster.SessionAffinity = ingressContext.Options.SessionAffinity;
        cluster.HealthCheck = ingressContext.Options.HealthCheck;
        cluster.HttpClientConfig = ingressContext.Options.HttpClientConfig;

        // make sure cluster is present
        foreach (var subset in subsets ?? Enumerable.Empty<V1EndpointSubset>())
        {
            foreach (var port in subset.Ports ?? Enumerable.Empty<Corev1EndpointPort>())
            {
                if (!MatchesPort(port, servicePort.TargetPort))
                {
                    continue;
                }

                var pathMatch = FixupPathMatch(path);
                var host = rule.Host;

                routes.Add(new RouteConfig()
                {
                    Match = new RouteMatch()
                    {
                        Hosts = host is not null ? new[] { host } : Array.Empty<string>(),
                        Path = pathMatch
                    },
                    ClusterId = cluster.ClusterId,
                    RouteId = $"{ingressContext.Ingress.Metadata.Name}.{ingressContext.Ingress.Metadata.NamespaceProperty}:{path.Path}",
                    Transforms = ingressContext.Options.Transforms,
                    AuthorizationPolicy = ingressContext.Options.AuthorizationPolicy,
                    CorsPolicy = ingressContext.Options.CorsPolicy,
                    Metadata = ingressContext.Options.RouteMetadata,
                });

                // Add destination for every endpoint address
                foreach (var address in subset.Addresses ?? Enumerable.Empty<V1EndpointAddress>())
                {
                    var protocol = ingressContext.Options.Https ? "https" : "http";
                    var uri = $"{protocol}://{address.Ip}:{port.Port}";
                    cluster.Destinations[uri] = new DestinationConfig()
                    {
                        Address = uri
                    };
                }
            }
        }
    }

    private static string UpstreamName(string namespaceName, V1IngressServiceBackend ingressServiceBackend)
    {
        if (ingressServiceBackend is not null)
        {
            if (ingressServiceBackend.Port.Number.HasValue && ingressServiceBackend.Port.Number.Value > 0)
            {
                return $"{ingressServiceBackend.Name}.{namespaceName}:{ingressServiceBackend.Port.Number}";
            }

            if (!string.IsNullOrWhiteSpace(ingressServiceBackend.Port.Name))
            {
                return $"{ingressServiceBackend.Name}.{namespaceName}:{ingressServiceBackend.Port.Name}";
            }
        }

        return $"{namespaceName}-INVALID";
    }

    private static string FixupPathMatch(V1HTTPIngressPath path)
    {
        var pathMatch = path.Path;

        // Prefix match is the default for implementation specific.
        if (string.Equals(path.PathType, "Prefix", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path.PathType, "ImplementationSpecific", StringComparison.OrdinalIgnoreCase))
        {
            if (!pathMatch.EndsWith("/", StringComparison.Ordinal))
            {
                pathMatch += "/";
            }
            // remember for prefix matches, /foo/ works for either /foo or /foo/
            pathMatch += "{**catch-all}";
        }

        return pathMatch;
    }

    private static YarpIngressOptions HandleAnnotations(YarpIngressContext context, V1ObjectMeta metadata)
    {
        var options = context.Options;
        var annotations = metadata.Annotations;
        if (annotations is null)
        {
            return options;
        }

        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/backend-protocol", out var http))
        {
        	options.Https = http.Equals("https", StringComparison.OrdinalIgnoreCase);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/transforms", out var transforms))
        {
            options.Transforms = YamlDeserializer.Deserialize<List<Dictionary<string,string>>>(transforms);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/authorization-policy", out var authorizationPolicy))
        {
            options.AuthorizationPolicy = authorizationPolicy;
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/cors-policy", out var corsPolicy))
        {
            options.CorsPolicy = corsPolicy;
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/session-affinity", out var sessionAffinity))
        {
            options.SessionAffinity = YamlDeserializer.Deserialize<SessionAffinityConfig>(sessionAffinity);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/load-balancing", out var loadBalancing))
        {
            options.LoadBalancingPolicy = loadBalancing;
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/http-client", out var httpClientConfig))
        {
            options.HttpClientConfig = YamlDeserializer.Deserialize<HttpClientConfig>(httpClientConfig);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/health-check", out var healthCheck))
        {
            options.HealthCheck = YamlDeserializer.Deserialize<HealthCheckConfig>(healthCheck);
        }
        if (annotations.TryGetValue("yarp.ingress.kubernetes.io/route-metadata", out var routeMetadata))
        {
            options.RouteMetadata = YamlDeserializer.Deserialize<Dictionary<string, string>>(routeMetadata);
        }

        // metadata to support:
        // rewrite target
        // auth
        // http or https
        // default backend
        // CORS
        // GRPC
        // HTTP2
        // Conneciton limits
        // rate limits

        // backend health checks.
        return options;
    }

    private static bool MatchesPort(Corev1EndpointPort port1, IntstrIntOrString port2)
    {
        if (port1 is null || port2 is null)
        {
            return false;
        }
        if (int.TryParse(port2, out var port2Number) && port2Number == port1.Port)
        {
            return true;
        }
        if (string.Equals(port2, port1.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    private static bool MatchesPort(V1ServicePort port1, V1ServiceBackendPort port2)
    {
        if (port1 is null || port2 is null)
        {
            return false;
        }
        if (port2.Number is not null && port2.Number == port1.Port)
        {
            return true;
        }
        if (port2.Name is not null && string.Equals(port2.Name, port1.Name, StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }
}
