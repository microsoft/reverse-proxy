using System;
using System.Collections.Generic;
using System.Linq;
using k8s.Models;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Kubernetes.Controller.Caching;
using Yarp.ReverseProxy.Kubernetes.Controller.Services;

namespace Yarp.ReverseProxy.Kubernetes.Controller.Converters
{
    internal static class YarpParser
    {
        internal static void CovertFromKubernetesIngress(YarpIngressContext context)
        {
            var spec = context.Ingress.Spec;
            var defaultBackend = spec?.DefaultBackend;
            var defaultService = defaultBackend?.Service;
            IList<V1EndpointSubset> defaultSubsets = default;

            if (!string.IsNullOrEmpty(defaultService?.Name))
            {
                defaultSubsets = context.Endpoints.SingleOrDefault(x => x.Name == defaultService?.Name).Subsets;
            }

            // cluster can contain multiple replicas for each destination, need to know the lookup base don endpoints
            var options = HandleAnnotations(context, context.Ingress.Metadata);

            foreach (var rule in spec.Rules ?? Enumerable.Empty<V1IngressRule>())
            {
                HandleIngressRule(context, context.Endpoints, defaultSubsets, rule);
            }

            CreateClusters(context);
        }

        private static void CreateClusters(YarpIngressContext context)
        {
            foreach (var cluster in context.ClusterTransfers)
            {
                context.Clusters.Add(new ClusterConfig()
                {
                    Destinations = cluster.Value.Destinations,
                    ClusterId = cluster.Value.ClusterId
                });
            }
        }

        private static void HandleIngressRule(YarpIngressContext context, List<Endpoints> endpoints, IList<V1EndpointSubset> defaultSubsets, V1IngressRule rule)
        {
            var http = rule.Http;
            foreach (var path in http.Paths ?? Enumerable.Empty<V1HTTPIngressPath>())
            {
                var service = context.Services.SingleOrDefault(s => s.Metadata.Name == path.Backend.Service.Name);
                var servicePort = service.Spec.Ports.SingleOrDefault(p => MatchesPort(p, path.Backend.Service.Port));
                HandleIngressRulePath(context, servicePort, endpoints, defaultSubsets, rule, path);
            }
        }

        private static void HandleIngressRulePath(YarpIngressContext context, V1ServicePort servicePort, List<Endpoints> endpoints, IList<V1EndpointSubset> defaultSubsets, V1IngressRule rule, V1HTTPIngressPath path)
        {
            var backend = path.Backend;
            var ingressServiceBackend = backend.Service;
            var subsets = defaultSubsets;

            var clusters = context.ClusterTransfers;
            var routes = context.Routes;

            if (!string.IsNullOrEmpty(ingressServiceBackend?.Name))
            {
                subsets = endpoints.SingleOrDefault(x => x.Name == ingressServiceBackend?.Name).Subsets;
            }

            // Each ingress rule path can only be for one service
            var key = ingressServiceBackend.Port.Number.HasValue ? $"{ingressServiceBackend?.Name}:{ingressServiceBackend?.Port.Number}" : $"{ingressServiceBackend?.Name}:{ingressServiceBackend?.Port.Name}";
            if (!clusters.ContainsKey(key))
            {
                clusters.Add(key, new ClusterTransfer());
            }

            var cluster = clusters[key];
            cluster.ClusterId = key;

            // make sure cluster is present
            foreach (var subset in subsets ?? Enumerable.Empty<V1EndpointSubset>())
            {
                foreach (var port in subset.Ports ?? Enumerable.Empty<V1EndpointPort>())
                {
                    foreach (var address in subset.Addresses ?? Enumerable.Empty<V1EndpointAddress>())
                    {
                        if (!MatchesPort(port, servicePort.TargetPort))
                        {
                            continue;
                        }

                        var protocol = context.Options.Https ? "https" : "http";
                        var uri = $"{protocol}://{address.Ip}:{port.Port}";
                        cluster.Destinations[uri] = new DestinationConfig()
                        {
                            Address = uri
                        };

                        var pathMatch = FixupPathMatch(path);
                        var host = rule.Host;

                        routes.Add(new RouteConfig()
                        {
                            Match = new RouteMatch()
                            {
                                Hosts = host != null ? new[] { host } : Array.Empty<string>(),
                                Path = pathMatch
                            },
                            ClusterId = cluster.ClusterId,
                            RouteId = path.Path
                        });
                    }
                }
            }
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
            if (annotations == null)
            {
                return options;
            }

            if (annotations.TryGetValue("yarp.ingress.kubernetes.io/backend-protocol", out var http))
            {
                options.Https = http.Equals("https", StringComparison.OrdinalIgnoreCase);
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

        private static bool MatchesPort(V1EndpointPort port1, IntstrIntOrString port2)
        {
            if (port1 == null || port2 == null)
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
            if (port1 == null || port2 == null)
            {
                return false;
            }
            if (port2.Number != null && port2.Number == port1.Port)
            {
                return true;
            }
            if (port2.Name != null && string.Equals(port2.Name, port1.Name, StringComparison.Ordinal))
            {
                return true;
            }
            return false;
        }
    }
}
