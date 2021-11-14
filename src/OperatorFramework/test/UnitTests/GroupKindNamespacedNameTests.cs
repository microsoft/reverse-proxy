// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Xunit;

namespace Microsoft.Kubernetes;

public class GroupKindNamespacedNameTests
{
    [Fact]
    public void GroupKindAndNamespacedNameFromResource()
    {
        var resource = new V1Role(
            apiVersion: $"{V1Role.KubeGroup}/{V1Role.KubeApiVersion}",
            kind: V1Role.KubeKind,
            metadata: new V1ObjectMeta(
                name: "the-name",
                namespaceProperty: "the-namespace"));

        var key = GroupKindNamespacedName.From(resource);

        Assert.Equal("rbac.authorization.k8s.io", key.Group);
        Assert.Equal("Role", key.Kind);
        Assert.Equal("the-namespace", key.NamespacedName.Namespace);
        Assert.Equal("the-name", key.NamespacedName.Name);
    }

    [Fact]
    public void GroupCanBeEmpty()
    {
        var resource = new V1ConfigMap(
            apiVersion: V1ConfigMap.KubeApiVersion,
            kind: V1ConfigMap.KubeKind,
            metadata: new V1ObjectMeta(
                name: "the-name",
                namespaceProperty: "the-namespace"));

        var key = GroupKindNamespacedName.From(resource);

        Assert.Equal("", key.Group);
        Assert.Equal("ConfigMap", key.Kind);
        Assert.Equal("the-namespace", key.NamespacedName.Namespace);
        Assert.Equal("the-name", key.NamespacedName.Name);
    }

    [Fact]
    public void NamespaceCanBeNull()
    {
        var resource = new V1ClusterRole(
            apiVersion: $"{V1ClusterRole.KubeGroup}/{V1ClusterRole.KubeApiVersion}",
            kind: V1ClusterRole.KubeKind,
            metadata: new V1ObjectMeta(
                name: "the-name"));

        var key = GroupKindNamespacedName.From(resource);

        Assert.Equal("rbac.authorization.k8s.io", key.Group);
        Assert.Equal("ClusterRole", key.Kind);
        Assert.Null(key.NamespacedName.Namespace);
        Assert.Equal("the-name", key.NamespacedName.Name);
    }

    [Theory]
    [InlineData("group", "kind", "ns", "name", "group", "kind", "ns", "name", true)]
    [InlineData("group", "kind", null, "name", "group", "kind", null, "name", true)]
    [InlineData("", "kind", "ns", "name", "", "kind", "ns", "name", true)]
    [InlineData("", "kind", null, "name", "", "kind", null, "name", true)]
    [InlineData("group", "kind", "ns", "name", "group2", "kind", "ns", "name", false)]
    [InlineData("group", "kind", "ns", "name", "group", "kind2", "ns", "name", false)]
    [InlineData("group", "kind", "ns", "name", "group", "kind", "ns2", "name", false)]
    [InlineData("group", "kind", "ns", "name", "group", "kind", null, "name", false)]
    [InlineData("group", "kind", "ns", "name", "group", "kind", "ns", "name2", false)]
    public void EqualityAndInequality(
        string group1,
        string kind1,
        string ns1,
        string name1,
        string group2,
        string kind2,
        string ns2,
        string name2,
        bool shouldBeEqual)
    {
        var key1 = new GroupKindNamespacedName(group1, kind1, new NamespacedName(ns1, name1));
        var key2 = new GroupKindNamespacedName(group2, kind2, new NamespacedName(ns2, name2));

        var areEqual = key1 == key2;
        var areNotEqual = key1 != key2;
#pragma warning disable CS1718 // Comparison made to same variable
        var sameEqual1 = key1 == key1;
        var sameNotEqual1 = key1 != key1;
        var sameEqual2 = key2 == key2;
        var sameNotEqual2 = key2 != key2;
#pragma warning restore CS1718 // Comparison made to same variable

        Assert.NotEqual(areNotEqual, areEqual);
        Assert.Equal(shouldBeEqual, areEqual);
        Assert.True(sameEqual1);
        Assert.False(sameNotEqual1);
        Assert.True(sameEqual2);
        Assert.False(sameNotEqual2);
    }
}
