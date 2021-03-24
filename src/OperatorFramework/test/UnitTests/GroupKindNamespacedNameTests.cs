// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Microsoft.Kubernetes
{
    [TestClass]
    public class GroupKindNamespacedNameTests
    {

        [TestMethod]
        public void GroupKindAndNamespacedNameFromResource()
        {
            // arrange
            var resource = new V1Role(
                apiVersion: $"{V1Role.KubeGroup}/{V1Role.KubeApiVersion}",
                kind: V1Role.KubeKind,
                metadata: new V1ObjectMeta(
                    name: "the-name",
                    namespaceProperty: "the-namespace"));

            // act
            var key = GroupKindNamespacedName.From(resource);

            // assert
            key.Group.ShouldBe("rbac.authorization.k8s.io");
            key.Kind.ShouldBe("Role");
            key.NamespacedName.Namespace.ShouldBe("the-namespace");
            key.NamespacedName.Name.ShouldBe("the-name");
        }

        [TestMethod]
        public void GroupCanBeEmpty()
        {
            // arrange
            var resource = new V1ConfigMap(
                apiVersion: V1ConfigMap.KubeApiVersion,
                kind: V1ConfigMap.KubeKind,
                metadata: new V1ObjectMeta(
                    name: "the-name",
                    namespaceProperty: "the-namespace"));

            // act
            var key = GroupKindNamespacedName.From(resource);

            // assert
            key.Group.ShouldBe("");
            key.Kind.ShouldBe("ConfigMap");
            key.NamespacedName.Namespace.ShouldBe("the-namespace");
            key.NamespacedName.Name.ShouldBe("the-name");
        }

        [TestMethod]
        public void NamespaceCanBeNull()
        {
            // arrange
            var resource = new V1ClusterRole(
                apiVersion: $"{V1ClusterRole.KubeGroup}/{V1ClusterRole.KubeApiVersion}",
                kind: V1ClusterRole.KubeKind,
                metadata: new V1ObjectMeta(
                    name: "the-name"));

            // act
            var key = GroupKindNamespacedName.From(resource);

            // assert
            key.Group.ShouldBe("rbac.authorization.k8s.io");
            key.Kind.ShouldBe("ClusterRole");
            key.NamespacedName.Namespace.ShouldBeNull();
            key.NamespacedName.Name.ShouldBe("the-name");
        }

        [TestMethod]
        [DataRow("group", "kind", "ns", "name", "group", "kind", "ns", "name", true)]
        [DataRow("group", "kind", null, "name", "group", "kind", null, "name", true)]
        [DataRow("", "kind", "ns", "name", "", "kind", "ns", "name", true)]
        [DataRow("", "kind", null, "name", "", "kind", null, "name", true)]
        [DataRow("group", "kind", "ns", "name", "group2", "kind", "ns", "name", false)]
        [DataRow("group", "kind", "ns", "name", "group", "kind2", "ns", "name", false)]
        [DataRow("group", "kind", "ns", "name", "group", "kind", "ns2", "name", false)]
        [DataRow("group", "kind", "ns", "name", "group", "kind", null, "name", false)]
        [DataRow("group", "kind", "ns", "name", "group", "kind", "ns", "name2", false)]
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
            // arrange
            var key1 = new GroupKindNamespacedName(group1, kind1, new NamespacedName(ns1, name1));
            var key2 = new GroupKindNamespacedName(group2, kind2, new NamespacedName(ns2, name2));

            // act
            var areEqual = key1 == key2;
            var areNotEqual = key1 != key2;
#pragma warning disable CS1718 // Comparison made to same variable
            var sameEqual1 = key1 == key1;
            var sameNotEqual1 = key1 != key1;
            var sameEqual2 = key2 == key2;
            var sameNotEqual2 = key2 != key2;
#pragma warning restore CS1718 // Comparison made to same variable

            // assert
            areEqual.ShouldNotBe(areNotEqual);
            areEqual.ShouldBe(shouldBeEqual);
            sameEqual1.ShouldBeTrue();
            sameNotEqual1.ShouldBeFalse();
            sameEqual2.ShouldBeTrue();
            sameNotEqual2.ShouldBeFalse();
        }
    }
}
