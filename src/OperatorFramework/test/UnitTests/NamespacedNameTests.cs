// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System.Collections.Generic;

namespace Microsoft.Kubernetes
{
    [TestClass]
    public class NamespacedNameTests
    {
        [TestMethod]
        public void WorksAsDictionaryKey()
        {
            // arrange
            var dictionary = new Dictionary<NamespacedName, string>();
            var name1 = new NamespacedName("ns", "n1");
            var name2 = new NamespacedName("ns", "n2");
            var name3 = new NamespacedName("ns", "n3");

            // act
            dictionary[name1] = "one";
            dictionary[name1] = "one again";
            dictionary[name2] = "two";

            // assert
            dictionary.ShouldSatisfyAllConditions(
                () => dictionary.ShouldContainKeyAndValue(name1, "one again"),
                () => dictionary.ShouldContainKeyAndValue(name2, "two"),
                () => dictionary.ShouldNotContainKey(name3));
        }

        [TestMethod]
        [DataRow("ns", "n1", "ns", "n1", true)]
        [DataRow("ns", "n1", "ns", "n2", false)]
        [DataRow("ns", "n1", "ns-x", "n1", false)]
        [DataRow(null, "n1", null, "n1", true)]
        [DataRow(null, "n1", null, "n2", false)]
        public void EqualityAndInequality(
            string namespace1,
            string name1,
            string namespace2,
            string name2,
            bool shouldBeEqual)
        {
            // arrange
            var namespacedName1 = new NamespacedName(namespace1, name1);
            var namespacedName2 = new NamespacedName(namespace2, name2);

            // act
            var areEqual = namespacedName1 == namespacedName2;
            var areNotEqual = namespacedName1 != namespacedName2;
#pragma warning disable CS1718 // Comparison made to same variable
            var sameEqual1 = namespacedName1 == namespacedName1;
            var sameNotEqual1 = namespacedName1 != namespacedName1;
            var sameEqual2 = namespacedName2 == namespacedName2;
            var sameNotEqual2 = namespacedName2 != namespacedName2;
#pragma warning restore CS1718 // Comparison made to same variable

            // assert
            areEqual.ShouldNotBe(areNotEqual);
            areEqual.ShouldBe(shouldBeEqual);
            sameEqual1.ShouldBeTrue();
            sameNotEqual1.ShouldBeFalse();
            sameEqual2.ShouldBeTrue();
            sameNotEqual2.ShouldBeFalse();
        }

        [TestMethod]
        public void NamespaceAndNameFromResource()
        {
            // arrange
            var resource = new V1ConfigMap(
                apiVersion: V1ConfigMap.KubeApiVersion,
                kind: V1ConfigMap.KubeKind,
                metadata: new V1ObjectMeta(
                    name: "the-name",
                    namespaceProperty: "the-namespace"));

            // act
            var nn = NamespacedName.From(resource);

            // assert
            nn.Name.ShouldBe("the-name");
            nn.Namespace.ShouldBe("the-namespace");
        }

        [TestMethod]
        public void JustNameFromClusterResource()
        {
            // arrange
            var resource = new V1ClusterRole(
                apiVersion: V1ClusterRole.KubeApiVersion,
                kind: V1ClusterRole.KubeKind,
                metadata: new V1ObjectMeta(
                    name: "the-name"));

            // act
            var nn = NamespacedName.From(resource);

            // assert
            nn.Name.ShouldBe("the-name");
            nn.Namespace.ShouldBeNull();
        }
    }
}
