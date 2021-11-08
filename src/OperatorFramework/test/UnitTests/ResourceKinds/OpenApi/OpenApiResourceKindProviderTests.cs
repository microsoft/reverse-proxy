// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Kubernetes.ResourceKinds.OpenApi
{
    public class OpenApiResourceKindProviderTests
    {
        public static OpenApiResourceKindProvider SharedProvider { get; set; } = new(new FakeLogger<OpenApiResourceKindProvider>());

        [Theory]
        [InlineData("v1", "Pod")]
        [InlineData("rbac.authorization.k8s.io/v1", "RoleBinding")]
        public async Task BuiltInResourceKindsCanBeFound(string apiVersion, string kind)
        {
            var provider = SharedProvider;

            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);

            Assert.NotNull(resourceKind);
            Assert.Equal(apiVersion, resourceKind.ApiVersion);
            Assert.Equal(kind, resourceKind.Kind);
            Assert.NotNull(resourceKind.Schema);
            Assert.Equal(ElementMergeStrategy.MergeObject, resourceKind.Schema.MergeStrategy);
        }

        [Theory]
        [InlineData("v1", "Pod")]
        [InlineData("rbac.authorization.k8s.io/v1", "RoleBinding")]
        public async Task UnknownPropertiesComeBackAsMergeStrategyUnknown(string apiVersion, string kind)
        {
            var provider = SharedProvider;

            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);

            Assert.NotNull(resourceKind.Schema);
            var property = resourceKind.Schema.GetPropertyElementType("badPropertyName");
            Assert.NotNull(property);
            Assert.Equal(ElementMergeStrategy.Unknown, property.MergeStrategy);
        }

        [Theory]
        [InlineData("v1", "Pod")]
        [InlineData("rbac.authorization.k8s.io/v1", "RoleBinding")]
        public async Task ApiVersionAndKindArePrimative(string apiVersion, string kind)
        {
            var provider = SharedProvider;

            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);

            Assert.NotNull(resourceKind.Schema);

            var apiVersionProperty = resourceKind.Schema.GetPropertyElementType("apiVersion");
            Assert.NotNull(apiVersionProperty);
            Assert.Equal(ElementMergeStrategy.ReplacePrimative, apiVersionProperty.MergeStrategy);

            var kindProperty = resourceKind.Schema.GetPropertyElementType("kind");
            Assert.NotNull(kindProperty);
            Assert.Equal(ElementMergeStrategy.ReplacePrimative, kindProperty.MergeStrategy);
        }

        [Theory]
        [InlineData("v1", "Pod")]
        [InlineData("rbac.authorization.k8s.io/v1", "RoleBinding")]
        public async Task MetadataNameAndNamespaceArePrimative(string apiVersion, string kind)
        {
            var provider = SharedProvider;

            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);

            Assert.NotNull(resourceKind);
            Assert.NotNull(resourceKind.Schema);
            var metadata = resourceKind.Schema.GetPropertyElementType("metadata");
            Assert.NotNull(metadata);
            Assert.Equal(ElementMergeStrategy.MergeObject, metadata.MergeStrategy);

            var name = metadata.GetPropertyElementType("name");
            Assert.NotNull(name);
            Assert.Equal(ElementMergeStrategy.ReplacePrimative, name.MergeStrategy);

            var @namespace = metadata.GetPropertyElementType("namespace");
            Assert.NotNull(@namespace);
            Assert.Equal(ElementMergeStrategy.ReplacePrimative, @namespace.MergeStrategy);
        }

        [Fact]
        public async Task ResourceKindAreCachedByAtProviderLevel()
        {
            var provider1 = new OpenApiResourceKindProvider(new FakeLogger<OpenApiResourceKindProvider>());
            var provider2 = new OpenApiResourceKindProvider(new FakeLogger<OpenApiResourceKindProvider>());

            var pod1a = await provider1.GetResourceKindAsync("v1", "Pod");
            var pod1b = await provider1.GetResourceKindAsync("v1", "Pod");
            var pod2a = await provider2.GetResourceKindAsync("v1", "Pod");
            var pod2b = await provider2.GetResourceKindAsync("v1", "Pod");

            Assert.Same(pod1b, pod1a);
            Assert.Same(pod2b, pod2a);
            Assert.NotSame(pod2a, pod1a);
            Assert.NotSame(pod2b, pod1a);
            Assert.NotSame(pod2a, pod1b);
            Assert.NotSame(pod2b, pod1b);
        }

        [Fact]
        public async Task MergeKeyAttributesAreRecognized()
        {
            var provider = SharedProvider;

            var pod = await provider.GetResourceKindAsync("v1", "Pod");

            Assert.NotNull(pod);
            Assert.NotNull(pod.Schema);
            var spec = pod.Schema.GetPropertyElementType("spec");
            Assert.NotNull(spec);
            var containers = spec.GetPropertyElementType("containers");
            Assert.NotNull(containers);

            Assert.Equal(ElementMergeStrategy.MergeListOfObject, containers.MergeStrategy);
            Assert.Equal("name", containers.MergeKey);
        }

        [Fact]
        public async Task ArrayOfPrimativeWithoutExtensionsIsReplaceListOfPrimative()
        {
            var provider = SharedProvider;

            var pod = await provider.GetResourceKindAsync("v1", "Pod");

            Assert.NotNull(pod);
            Assert.NotNull(pod.Schema);
            var spec = pod.Schema.GetPropertyElementType("spec");
            Assert.NotNull(spec);
            var containers = spec.GetPropertyElementType("containers");
            Assert.NotNull(containers);
            var containersCollection = containers.GetCollectionElementType();
            Assert.NotNull(containersCollection);
            var args = containersCollection.GetPropertyElementType("args");

            Assert.Equal(ElementMergeStrategy.ReplaceListOfPrimative, args.MergeStrategy);
        }

        [Fact]
        public async Task ArrayOfPrimativeCanHaveMergeExtensions()
        {
            var provider = SharedProvider;

            var pod = await provider.GetResourceKindAsync("v1", "Pod");

            Assert.NotNull(pod);
            Assert.NotNull(pod.Schema);
            var metadata = pod.Schema.GetPropertyElementType("metadata");
            Assert.NotNull(metadata);
            var finalizers = metadata.GetPropertyElementType("finalizers");
            Assert.NotNull(finalizers);

            Assert.Equal(ElementMergeStrategy.MergeListOfPrimative, finalizers.MergeStrategy);
        }

        [Theory]
        [InlineData("v2", "Pod")]
        [InlineData("v1", "FluxCapacitor")]
        public async Task NotFoundResourceKindComesProducesNull(string apiVersion, string kind)
        {
            var provider = SharedProvider;

            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);

            Assert.Null(resourceKind);
        }

        [Fact]
        public async Task DictionaryHasInformationAboutContents()
        {
            var provider = SharedProvider;

            var pod = await provider.GetResourceKindAsync("v1", "Pod");
            var labels = pod.Schema.GetPropertyElementType("metadata").GetPropertyElementType("labels");

            Assert.Equal(ElementMergeStrategy.MergeMap, labels.MergeStrategy);
            Assert.Equal(ElementMergeStrategy.ReplacePrimative, labels.GetCollectionElementType().MergeStrategy);
        }
    }
}
