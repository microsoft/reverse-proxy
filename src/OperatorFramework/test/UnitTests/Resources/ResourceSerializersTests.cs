// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Shouldly;
using System.Collections.Generic;

namespace Microsoft.Kubernetes.Resources
{
    [TestClass]
    public class ResourceSerializersTests
    {
        private IResourceSerializers Serializers { get; } = new ResourceSerializers();

        [TestMethod]
        public void ResourceSerializesToJson()
        {
            // arrange
            var resource = new V1Role(
                apiVersion: $"{V1Role.KubeGroup}/{V1Role.KubeApiVersion}",
                kind: V1Role.KubeKind,
                metadata: new V1ObjectMeta(
                    namespaceProperty: "the-namespace",
                    name: "the-name"),
                rules: new[]
                {
                    new V1PolicyRule(
                        resourceNames: new []{"*"},
                        verbs: new []{"*"}),
                });

            // act
            var json = Serializers.SerializeJson(resource);

            // assert
            json.ShouldContain(@"""kind"":""Role""");
            json.ShouldContain(@"""apiVersion"":""rbac.authorization.k8s.io/v1""");
            json.ShouldContain(@"""name"":""the-name""");
            json.ShouldContain(@"""namespace"":""the-namespace""");
            json.ShouldContain(@"""resourceNames"":[""*""]");
            json.ShouldContain(@"""verbs"":[""*""]");
        }

        [TestMethod]
        public void DictionarySerializesToJson()
        {
            // arrange
            var dictionary = new Dictionary<string, object> {
                { "apiVersion", $"{V1Role.KubeGroup}/{V1Role.KubeApiVersion}" },
                { "kind", V1Role.KubeKind },
                { "metadata", new Dictionary<string, object>{
                    { "name", "the-name" } ,
                    { "namespace", "the-namespace" } ,
                }},
                { "rules", new List<object>{
                    new Dictionary<string, object>{
                        { "resourceNames", new List<object> { "*" } },
                        { "verbs", new List<object> { "*" } },
                    },
                }},
            };

            // act
            var json = Serializers.SerializeJson(dictionary);

            // assert
            json.ShouldContain(@"""kind"":""Role""");
            json.ShouldContain(@"""apiVersion"":""rbac.authorization.k8s.io/v1""");
            json.ShouldContain(@"""name"":""the-name""");
            json.ShouldContain(@"""namespace"":""the-namespace""");
            json.ShouldContain(@"""resourceNames"":[""*""]");
            json.ShouldContain(@"""verbs"":[""*""]");
        }

        [TestMethod]
        public void DeserializeJsonToResource()
        {
            // arrange
            var json = $@"
{{
    ""apiVersion"": ""{V1Role.KubeGroup}/{V1Role.KubeApiVersion}"",
    ""kind"": ""Role"",
    ""metadata"": {{
        ""name"": ""the-name"",
        ""namespace"": ""the-namespace""
    }},
    ""rules"": [{{
        ""resourceNames"": [""*""],
        ""verbs"": [""*""]
    }}]
}}
";

            // act
            var role = Serializers.DeserializeJson<V1Role>(json);

            // assert
            role.ApiGroupAndVersion().ShouldBe(("rbac.authorization.k8s.io", "v1"));
            role.Kind.ShouldBe("Role");
            role.Name().ShouldBe("the-name");
            role.Namespace().ShouldBe("the-namespace");
            var rule = role.Rules.ShouldHaveSingleItem();
            rule.ResourceNames.ShouldBe(new[] { "*" });
            rule.Verbs.ShouldBe(new[] { "*" });
        }

        [TestMethod]
        public void DeserializeYamlToResource()
        {
            // arrange
            var yaml = $@"
apiVersion: {V1Role.KubeGroup}/{V1Role.KubeApiVersion}
kind: Role
metadata: 
    name: the-name
    namespace: the-namespace
rules:
- resourceNames:
  - ""*""
  verbs:
  - ""*""
";

            // act
            var role = Serializers.DeserializeYaml<V1Role>(yaml);

            // assert
            role.ApiGroupAndVersion().ShouldBe(("rbac.authorization.k8s.io", "v1"));
            role.Kind.ShouldBe("Role");
            role.Name().ShouldBe("the-name");
            role.Namespace().ShouldBe("the-namespace");
            var rule = role.Rules.ShouldHaveSingleItem();
            rule.ResourceNames.ShouldBe(new[] { "*" });
            rule.Verbs.ShouldBe(new[] { "*" });
        }

        [TestMethod]
        public void ConvertDictionaryToResource()
        {
            // arrange
            var dictionary = new Dictionary<string, object> {
                { "apiVersion", $"{V1Role.KubeGroup}/{V1Role.KubeApiVersion}" },
                { "kind", V1Role.KubeKind },
                { "metadata", new Dictionary<string, object>{
                    { "name", "the-name" } ,
                    { "namespace", "the-namespace" } ,
                }},
                { "rules", new List<object>{
                    new Dictionary<string, object>{
                        { "resourceNames", new List<object> { "*" } },
                        { "verbs", new List<object> { "*" } },
                    },
                }},
            };

            // act
            var role = Serializers.Convert<V1Role>(dictionary);

            // assert
            role.ApiGroupAndVersion().ShouldBe(("rbac.authorization.k8s.io", "v1"));
            role.Kind.ShouldBe("Role");
            role.Name().ShouldBe("the-name");
            role.Namespace().ShouldBe("the-namespace");
            var rule = role.Rules.ShouldHaveSingleItem();
            rule.ResourceNames.ShouldBe(new[] { "*" });
            rule.Verbs.ShouldBe(new[] { "*" });
        }

        [TestMethod]
        public void DeserializeUntypedYamlWithIntDoubleAndBool()
        {
            // arrange
            var yaml = @"
an-object:
    an-integer: 1
    a-float: 1.0
    a-bool: true
    a-string: 1.0.
";

            // act
            var data = Serializers.DeserializeYaml<object>(yaml);

            // assert
            var root = data.ShouldBeOfType<JObject>();
            var anObject = root["an-object"].ShouldBeOfType<JObject>();
            anObject["an-integer"].Type.ShouldBe(JTokenType.Integer);
            anObject["a-float"].Type.ShouldBe(JTokenType.Float);
            anObject["a-bool"].Type.ShouldBe(JTokenType.Boolean);
            anObject["a-string"].Type.ShouldBe(JTokenType.String);
        }
    }
}
