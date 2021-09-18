// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Kubernetes;
using Newtonsoft.Json.Linq;
using Xunit;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Converters;
using Yarp.Kubernetes.Controller.Services;

namespace IngressController.Tests;

public class IngressControllerTests
{
    [Theory]
    [InlineData("basic-ingress")]
    [InlineData("multiple-endpoints-ports")]
    [InlineData("https")]
    [InlineData("exact-match")]
    [InlineData("mapped-port")]
    [InlineData("hostname-routing")]
    [InlineData("multiple-ingresses")]
    [InlineData("multiple-ingresses-one-svc")]
    public async Task ParsingTests(string name)
    {
        var cache = await GetKubernetesInfo(name).ConfigureAwait(false);
        var configContext = new YarpConfigContext();
        var ingresses = cache.GetIngresses().ToArray();

        foreach (var ingress in ingresses)
        {
            if (cache.TryGetReconcileData(new NamespacedName(ingress.Metadata.NamespaceProperty, ingress.Metadata.Name), out var data))
            {
                var ingressContext = new YarpIngressContext(ingress, data.ServiceList, data.EndpointsList);
                YarpParser.ConvertFromKubernetesIngress(ingressContext, configContext);
            }
        }

        VerifyClusters(JsonSerializer.Serialize(configContext.BuildClusterConfig()), name);
        VerifyRoutes(JsonSerializer.Serialize(configContext.Routes), name);
    }

    private static void VerifyClusters(string clusterJson, string name)
    {
        VerifyJson(clusterJson, name, "clusters.json");
    }

    private static void VerifyRoutes(string routesJson, string name)
    {
        VerifyJson(routesJson, name, "routes.json");
    }

    private static void VerifyJson(string json, string name, string fileName)
    {
        var other = File.ReadAllText(Path.Combine("testassets", name, fileName));

        Assert.True(JToken.DeepEquals(JToken.Parse(json), JToken.Parse(other)));
    }

    private async Task<ICache> GetKubernetesInfo(string name)
    {
        var cache = new IngressCache();

        var typeMap = new Dictionary<string, Type>();
        typeMap.Add("networking.k8s.io/v1/Ingress", typeof(V1Ingress));
        typeMap.Add("v1/Service", typeof(V1Service));
        typeMap.Add("v1/Endpoints", typeof(V1Endpoints));

        var kubeObjects = await Yaml.LoadAllFromFileAsync(Path.Combine("testassets", name, "ingress.yaml"), typeMap).ConfigureAwait(false);
        foreach (var obj in kubeObjects)
        {
            if (obj is V1Ingress ingress)
            {
                cache.Update(WatchEventType.Added, ingress);
            }
            else if (obj is V1Service service)
            {
                cache.Update(WatchEventType.Added, service);
            }
            else if (obj is V1Endpoints endpoints)
            {
                cache.Update(WatchEventType.Added, endpoints);
            }
        }

        return cache;
    }
}
