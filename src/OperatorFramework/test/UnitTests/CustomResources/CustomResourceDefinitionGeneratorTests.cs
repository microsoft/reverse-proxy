// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.CustomResources
{
    [TestClass]
    public class CustomResourceDefinitionGeneratorTests
    {
        [TestMethod]
        public async Task MetadataNameComesFromPluralNameAndGroup()
        {
            // arrange 
            var generator = new CustomResourceDefinitionGenerator();

            // act
            var crd = await generator.GenerateCustomResourceDefinitionAsync<SimpleResource>("Namespaced");

            // assert
            crd.Name().ShouldBe("testkinds.test-group");
        }

        [TestMethod]
        public async Task ApiVersionAndKindAreCorrect()
        {
            // arrange 
            var generator = new CustomResourceDefinitionGenerator();

            // act
            var crd = await generator.GenerateCustomResourceDefinitionAsync<SimpleResource>("Namespaced");

            // assert
            crd.ApiGroupVersion().ShouldBe("v1");
            crd.ApiGroupVersion().ShouldBe(V1CustomResourceDefinition.KubeApiVersion);
            crd.ApiGroup().ShouldBe("apiextensions.k8s.io");
            crd.ApiGroup().ShouldBe(V1CustomResourceDefinition.KubeGroup);
            crd.Kind.ShouldBe("CustomResourceDefinition");
            crd.Kind.ShouldBe(V1CustomResourceDefinition.KubeKind);
            crd.Validate();
        }

        [TestMethod]
        [DataRow("Namespaced")]
        [DataRow("Cluster")]
        public async Task ScopeProvidedByGenerateParameter(string scope)
        {
            // arrange 
            var generator = new CustomResourceDefinitionGenerator();

            // act
            var crd = await generator.GenerateCustomResourceDefinitionAsync<SimpleResource>(scope);

            // assert
            crd.Spec.Scope.ShouldBe(scope);
        }

        [TestMethod]
        [DataRow(typeof(SimpleResource), "test-group", "TestKind", "testkinds")]
        [DataRow(typeof(AnotherResource), "another-group", "AnotherKind", "anotherkinds")]
        public async Task GroupAndNamesComesFromKubernetesEntityAttribute(Type resourceType, string group, string kind, string plural)
        {
            // arrange 
            var generator = new CustomResourceDefinitionGenerator();

            // act
            var crd = await generator.GenerateCustomResourceDefinitionAsync(resourceType, "Namespaced");

            // assert
            crd.Spec.Group.ShouldBe(group);
            crd.Spec.Names.Kind.ShouldBe(kind);
            crd.Spec.Names.Plural.ShouldBe(plural);
        }

        [TestMethod]
        [DataRow(typeof(SimpleResource), "test-version")]
        [DataRow(typeof(AnotherResource), "another-version")]
        public async Task CreateWithSingleVersionThatIsStoredAndServed(Type resourceType, string version)
        {
            // arrange 
            var generator = new CustomResourceDefinitionGenerator();

            // act
            var crd = await generator.GenerateCustomResourceDefinitionAsync(resourceType, "Namespaced");

            // assert
            var crdVersion = crd.Spec.Versions.ShouldHaveSingleItem();
            crdVersion.Name.ShouldBe(version);
            crdVersion.Served.ShouldBe(true);
            crdVersion.Storage.ShouldBe(true);
        }

        [TestMethod]
        public async Task TypicalResourceHasSchema()
        {
            // arrange 
            var generator = new CustomResourceDefinitionGenerator();

            // act
            var crd = await generator.GenerateCustomResourceDefinitionAsync<TypicalResource>("Namespaced");

            // assert
            var schema = crd
                .Spec.ShouldNotBeNull()
                .Versions.ShouldHaveSingleItem()
                .Schema.ShouldNotBeNull()
                .OpenAPIV3Schema.ShouldNotBeNull();

            schema.Properties.Keys.ShouldBe(new[] { "apiVersion", "kind", "metadata", "spec", "status" });

            schema.Properties["apiVersion"].Type.ShouldBe("string");
            schema.Properties["kind"].Type.ShouldBe("string");
            schema.Properties["metadata"].Type.ShouldBe("object");
            schema.Properties["spec"].Type.ShouldBe("object");
            schema.Properties["status"].Type.ShouldBe("object");
        }

        [TestMethod]
        public async Task DescriptionsComeFromDocComments()
        {
            // arrange 
            var generator = new CustomResourceDefinitionGenerator();

            // act
            var crd = await generator.GenerateCustomResourceDefinitionAsync<TypicalResource>("Namespaced");

            // assert
            var schema = crd
                .Spec.ShouldNotBeNull()
                .Versions.ShouldHaveSingleItem()
                .Schema.ShouldNotBeNull()
                .OpenAPIV3Schema.ShouldNotBeNull();

            schema.Description.ShouldNotBeNull().ShouldContain("TypicalResource doc comment");
            schema.Properties["spec"].Description.ShouldNotBeNull().ShouldContain("Spec doc comment");
            schema.Properties["status"].Description.ShouldNotBeNull().ShouldContain("Status doc comment");
        }
    }
}
