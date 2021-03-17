using System;
using System.Collections.Generic;
using IngressController.Converters;
using IngressController.Caching;
using Xunit;
using IngressController.Services;
using k8s;
using YamlDotNet.Serialization;
using k8s.Models;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace IngressController.Tests
{
    public class IngressControllerTests
    {
        [Theory]
        [InlineData("basic-ingress")]
        [InlineData("multiple-endpoints-ports")]
        [InlineData("https")]
        [InlineData("exact-match")]
        public async Task ParsingTests(string name)
        {
            var (ingress, endpoints) = await GetKubernetesInfo(name);
            var context = new YarpIngressContext(ingress, endpoints);

            YarpParser.CovertFromKubernetesIngress(context);

            VerifyClusters(JsonSerializer.Serialize(context.Clusters), name);
            VerifyRoutes(JsonSerializer.Serialize(context.Routes), name);
        }

        private void VerifyClusters(string clusterJson, string name)
        {
            VerifyJson(clusterJson, name, "clusters.json");
        }

        private void VerifyRoutes(string routesJson, string name)
        {
            VerifyJson(routesJson, name, "routes.json");
        }

        private void VerifyJson(string json, string name, string fileName)
        {
            var other = File.ReadAllText(Path.Combine("testassets", name, fileName));

            Assert.True(JToken.DeepEquals(JToken.Parse(json), JToken.Parse(other)));
        }

        private async Task<(IngressData, List<Endpoints>)> GetKubernetesInfo(string name)
        {
            var typeMap = new Dictionary<string, Type>();
            typeMap.Add("networking.k8s.io/v1/Ingress", typeof(V1Ingress));
            typeMap.Add("v1/Endpoints", typeof(V1Endpoints));

            var kubeObjects = await Yaml.LoadAllFromFileAsync(Path.Combine("testassets", name, "ingress.yaml"), typeMap);
            IngressData ingressData = default;
            var endpointList = new List<Endpoints>();
            foreach (var obj in kubeObjects)
            {
                if (obj is V1Ingress ingress)
                {
                    ingressData = new IngressData(ingress);
                }
                else if (obj is V1Endpoints endpoints)
                {
                    endpointList.Add(new Endpoints(endpoints));
                }
            }

            return (ingressData, endpointList);
        }
    }
}
