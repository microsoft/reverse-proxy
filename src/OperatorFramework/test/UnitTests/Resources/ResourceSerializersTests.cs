// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Kubernetes.Resources;

public class ResourceSerializersTests
{
    private IResourceSerializers Serializers { get; } = new ResourceSerializers();

    [Fact]
    public void ResourceSerializesToJson()
    {
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

        var json = Serializers.SerializeJson(resource);

        Assert.Contains(@"""kind"":""Role""", json, StringComparison.Ordinal);
        Assert.Contains(@"""apiVersion"":""rbac.authorization.k8s.io/v1""", json, StringComparison.Ordinal);
        Assert.Contains(@"""name"":""the-name""", json, StringComparison.Ordinal);
        Assert.Contains(@"""namespace"":""the-namespace""", json, StringComparison.Ordinal);
        Assert.Contains(@"""resourceNames"":[""*""]", json, StringComparison.Ordinal);
        Assert.Contains(@"""verbs"":[""*""]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DictionarySerializesToJson()
    {
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

        var json = Serializers.SerializeJson(dictionary);

        Assert.Contains(@"""kind"":""Role""", json, StringComparison.Ordinal);
        Assert.Contains(@"""apiVersion"":""rbac.authorization.k8s.io/v1""", json, StringComparison.Ordinal);
        Assert.Contains(@"""name"":""the-name""", json, StringComparison.Ordinal);
        Assert.Contains(@"""namespace"":""the-namespace""", json, StringComparison.Ordinal);
        Assert.Contains(@"""resourceNames"":[""*""]", json, StringComparison.Ordinal);
        Assert.Contains(@"""verbs"":[""*""]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DeserializeJsonToResource()
    {
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

        var role = Serializers.DeserializeJson<V1Role>(json);

        Assert.Equal(("rbac.authorization.k8s.io", "v1"), role.ApiGroupAndVersion());
        Assert.Equal("Role", role.Kind);
        Assert.Equal("the-name", role.Name());
        Assert.Equal("the-namespace", role.Namespace());
        var rule = Assert.Single(role.Rules);
        Assert.Equal(new[] { "*" }, rule.ResourceNames);
        Assert.Equal(new[] { "*" }, rule.Verbs);
    }

    [Fact]
    public void DeserializeYamlToResource()
    {
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

        var role = Serializers.DeserializeYaml<V1Role>(yaml);

        Assert.Equal(("rbac.authorization.k8s.io", "v1"), role.ApiGroupAndVersion());
        Assert.Equal("Role", role.Kind);
        Assert.Equal("the-name", role.Name());
        Assert.Equal("the-namespace", role.Namespace());
        var rule = Assert.Single(role.Rules);
        Assert.Equal(new[] { "*" }, rule.ResourceNames);
        Assert.Equal(new[] { "*" }, rule.Verbs);
    }

    [Fact]
    public void ConvertDictionaryToResource()
    {
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

        var role = Serializers.Convert<V1Role>(dictionary);

        Assert.Equal(("rbac.authorization.k8s.io", "v1"), role.ApiGroupAndVersion());
        Assert.Equal("Role", role.Kind);
        Assert.Equal("the-name", role.Name());
        Assert.Equal("the-namespace", role.Namespace());
        var rule = Assert.Single(role.Rules);
        Assert.Equal(new[] { "*" }, rule.ResourceNames);
        Assert.Equal(new[] { "*" }, rule.Verbs);
    }

    [Fact]
    public void DeserializeUntypedYamlWithIntDoubleAndBool()
    {
        var yaml = @"
an-object:
    an-integer: 1
    a-float: 1.0
    a-bool: true
    a-string: ""1.0.""
";

        var data = Serializers.DeserializeYaml<object>(yaml);

        var root = Assert.IsType<JObject>(data);
        var anObject = Assert.IsType<JObject>(root["an-object"]);
        Assert.Equal(JTokenType.Integer, anObject["an-integer"].Type);
        Assert.Equal(JTokenType.Float, anObject["a-float"].Type);
        Assert.Equal(JTokenType.Boolean, anObject["a-bool"].Type);
        Assert.Equal(JTokenType.String, anObject["a-string"].Type);
    }
}
