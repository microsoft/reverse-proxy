// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Kubernetes.CustomResources
{
    public class CustomResourceDefinitionGeneratorTests
    {
        [Fact]
        public async Task MetadataNameComesFromPluralNameAndGroup()
        {
            var generator = new CustomResourceDefinitionGenerator();

            var crd = await generator.GenerateCustomResourceDefinitionAsync<SimpleResource>("Namespaced");

            Assert.Equal("testkinds.test-group", crd.Name());
        }

        [Fact]
        public async Task ApiVersionAndKindAreCorrect()
        {
            var generator = new CustomResourceDefinitionGenerator();

            var crd = await generator.GenerateCustomResourceDefinitionAsync<SimpleResource>("Namespaced");

            Assert.Equal("v1", crd.ApiGroupVersion());
            Assert.Equal(V1CustomResourceDefinition.KubeApiVersion, crd.ApiGroupVersion());
            Assert.Equal("apiextensions.k8s.io", crd.ApiGroup());
            Assert.Equal(V1CustomResourceDefinition.KubeGroup, crd.ApiGroup());
            Assert.Equal("CustomResourceDefinition", crd.Kind);
            Assert.Equal(V1CustomResourceDefinition.KubeKind, crd.Kind);
            crd.Validate();
        }

        [Theory]
        [InlineData("Namespaced")]
        [InlineData("Cluster")]
        public async Task ScopeProvidedByGenerateParameter(string scope)
        {
            var generator = new CustomResourceDefinitionGenerator();

            var crd = await generator.GenerateCustomResourceDefinitionAsync<SimpleResource>(scope);

            Assert.Equal(scope, crd.Spec.Scope);
        }

        [Theory]
        [InlineData(typeof(SimpleResource), "test-group", "TestKind", "testkinds")]
        [InlineData(typeof(AnotherResource), "another-group", "AnotherKind", "anotherkinds")]
        public async Task GroupAndNamesComesFromKubernetesEntityAttribute(Type resourceType, string group, string kind, string plural)
        {
            var generator = new CustomResourceDefinitionGenerator();

            var crd = await generator.GenerateCustomResourceDefinitionAsync(resourceType, "Namespaced");

            Assert.Equal(group, crd.Spec.Group);
            Assert.Equal(kind, crd.Spec.Names.Kind);
            Assert.Equal(plural, crd.Spec.Names.Plural);
        }

        [Theory]
        [InlineData(typeof(SimpleResource), "test-version")]
        [InlineData(typeof(AnotherResource), "another-version")]
        public async Task CreateWithSingleVersionThatIsStoredAndServed(Type resourceType, string version)
        {
            var generator = new CustomResourceDefinitionGenerator();

            var crd = await generator.GenerateCustomResourceDefinitionAsync(resourceType, "Namespaced");

            var crdVersion = Assert.Single(crd.Spec.Versions);
            Assert.Equal(version, crdVersion.Name);
            Assert.True(crdVersion.Served);
            Assert.True(crdVersion.Storage);
        }

        [Fact]
        public async Task TypicalResourceHasSchema()
        {
            var generator = new CustomResourceDefinitionGenerator();

            var crd = await generator.GenerateCustomResourceDefinitionAsync<TypicalResource>("Namespaced");

            Assert.NotNull(crd.Spec);
            var version = Assert.Single(crd.Spec.Versions);
            Assert.NotNull(version.Schema);
            var schema = version.Schema.OpenAPIV3Schema;
            Assert.NotNull(schema);

            Assert.Equal(new[] { "apiVersion", "kind", "metadata", "spec", "status" }, schema.Properties.Keys);

            Assert.Equal("string", schema.Properties["apiVersion"].Type);
            Assert.Equal("string", schema.Properties["kind"].Type);
            Assert.Equal("object", schema.Properties["metadata"].Type);
            Assert.Equal("object", schema.Properties["spec"].Type);
            Assert.Equal("object", schema.Properties["status"].Type);
        }

        [Fact]
        public async Task DescriptionsComeFromDocComments()
        {
            var generator = new CustomResourceDefinitionGenerator();

            var crd = await generator.GenerateCustomResourceDefinitionAsync<TypicalResource>("Namespaced");

            Assert.NotNull(crd.Spec);
            var version = Assert.Single(crd.Spec.Versions);
            Assert.NotNull(version.Schema);
            var schema = version.Schema.OpenAPIV3Schema;
            Assert.NotNull(schema);

            Assert.Contains("TypicalResource doc comment", schema.Description, StringComparison.Ordinal);
            Assert.Contains("Spec doc comment", schema.Properties["spec"].Description, StringComparison.Ordinal);
            Assert.Contains("Status doc comment", schema.Properties["status"].Description, StringComparison.Ordinal);
        }
    }
}
