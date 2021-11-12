// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
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
    public async Task ParsingTests(string name)
    {
        var (ingress, services, endpoints) = await GetKubernetesInfo(name).ConfigureAwait(false);
        var context = new YarpIngressContext(ingress, services, endpoints);

        YarpParser.CovertFromKubernetesIngress(context);

        VerifyClusters(JsonSerializer.Serialize(context.Clusters), name);
        VerifyRoutes(JsonSerializer.Serialize(context.Routes), name);
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

    private async Task<(IngressData, List<ServiceData>, List<Endpoints>)> GetKubernetesInfo(string name)
    {
        var typeMap = new Dictionary<string, Type>();
        typeMap.Add("networking.k8s.io/v1/Ingress", typeof(V1Ingress));
        typeMap.Add("v1/Service", typeof(V1Service));
        typeMap.Add("v1/Endpoints", typeof(V1Endpoints));

        var kubeObjects = await Yaml.LoadAllFromFileAsync(Path.Combine("testassets", name, "ingress.yaml"), typeMap).ConfigureAwait(false);
        IngressData ingressData = default;
        var serviceList = new List<ServiceData>();
        var endpointList = new List<Endpoints>();
        foreach (var obj in kubeObjects)
        {
            if (obj is V1Ingress ingress)
            {
                ingressData = new IngressData(ingress);
            }
            else if (obj is V1Service service)
            {
                serviceList.Add(new ServiceData(service));
            }
            else if (obj is V1Endpoints endpoints)
            {
                endpointList.Add(new Endpoints(endpoints));
            }
        }

        return (ingressData, serviceList, endpointList);
    }
}
