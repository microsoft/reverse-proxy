// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.ResourceKinds.OpenApi
{
    [TestClass]
    public class OpenApiResourceKindProviderTests
    {
        public static OpenApiResourceKindProvider SharedProvider { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            if (testContext is null)
            {
                throw new System.ArgumentNullException(nameof(testContext));
            }

            SharedProvider = new OpenApiResourceKindProvider(new FakeLogger<OpenApiResourceKindProvider>());
        }

        [TestMethod]
        [DataRow("v1", "Pod")]
        [DataRow("rbac.authorization.k8s.io/v1", "RoleBinding")]
        public async Task BuiltInResourceKindsCanBeFound(string apiVersion, string kind)
        {
            // arrange
            var provider = SharedProvider;

            // act
            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);

            // assert
            resourceKind.ShouldNotBeNull();
            resourceKind.ApiVersion.ShouldBe(apiVersion);
            resourceKind.Kind.ShouldBe(kind);
            resourceKind.Schema.ShouldNotBeNull()
                .MergeStrategy.ShouldBe(ElementMergeStrategy.MergeObject);
        }



        [TestMethod]
        [DataRow("v1", "Pod")]
        [DataRow("rbac.authorization.k8s.io/v1", "RoleBinding")]
        public async Task UnknownPropertiesComeBackAsMergeStrategyUnknown(string apiVersion, string kind)
        {
            // arrange
            var provider = SharedProvider;

            // act
            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);

            // assert
            resourceKind.Schema.ShouldNotBeNull()
                .GetPropertyElementType("badPropertyName").ShouldNotBeNull()
                .MergeStrategy.ShouldBe(ElementMergeStrategy.Unknown);
        }

        [TestMethod]
        [DataRow("v1", "Pod")]
        [DataRow("rbac.authorization.k8s.io/v1", "RoleBinding")]
        public async Task ApiVersionAndKindArePrimative(string apiVersion, string kind)
        {
            // arrange
            var provider = SharedProvider;

            // act
            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);

            // assert
            resourceKind.Schema.ShouldNotBeNull()
                .GetPropertyElementType("apiVersion").ShouldNotBeNull()
                .MergeStrategy.ShouldBe(ElementMergeStrategy.ReplacePrimative);

            resourceKind.Schema.ShouldNotBeNull()
                .GetPropertyElementType("kind").ShouldNotBeNull()
                .MergeStrategy.ShouldBe(ElementMergeStrategy.ReplacePrimative);
        }

        [TestMethod]
        [DataRow("v1", "Pod")]
        [DataRow("rbac.authorization.k8s.io/v1", "RoleBinding")]
        public async Task MetadataNameAndNamespaceArePrimative(string apiVersion, string kind)
        {
            // arrange
            var provider = SharedProvider;

            // act
            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);

            // assert
            resourceKind.Schema.ShouldNotBeNull()
                .GetPropertyElementType("metadata").ShouldNotBeNull()
                .MergeStrategy.ShouldBe(ElementMergeStrategy.MergeObject);

            resourceKind.Schema.ShouldNotBeNull()
                .GetPropertyElementType("metadata").ShouldNotBeNull()
                .GetPropertyElementType("name").ShouldNotBeNull()
                .MergeStrategy.ShouldBe(ElementMergeStrategy.ReplacePrimative);

            resourceKind.Schema.ShouldNotBeNull()
                .GetPropertyElementType("metadata").ShouldNotBeNull()
                .GetPropertyElementType("namespace").ShouldNotBeNull()
                .MergeStrategy.ShouldBe(ElementMergeStrategy.ReplacePrimative);
        }

        [TestMethod]
        public async Task ResourceKindAreCachedByAtProviderLevel()
        {
            // arrange
            var provider1 = new OpenApiResourceKindProvider(new FakeLogger<OpenApiResourceKindProvider>());
            var provider2 = new OpenApiResourceKindProvider(new FakeLogger<OpenApiResourceKindProvider>());

            // act
            var pod1a = await provider1.GetResourceKindAsync("v1", "Pod");
            var pod1b = await provider1.GetResourceKindAsync("v1", "Pod");
            var pod2a = await provider2.GetResourceKindAsync("v1", "Pod");
            var pod2b = await provider2.GetResourceKindAsync("v1", "Pod");

            // assert
            pod1a.ShouldBeSameAs(pod1b);
            pod2a.ShouldBeSameAs(pod2b);
            pod1a.ShouldNotBeSameAs(pod2a);
            pod1a.ShouldNotBeSameAs(pod2b);
            pod1b.ShouldNotBeSameAs(pod2a);
            pod1b.ShouldNotBeSameAs(pod2b);
        }

        [TestMethod]
        public async Task MergeKeyAttributesAreRecognized()
        {
            // arrange
            var provider = SharedProvider;

            // act
            var pod = await provider.GetResourceKindAsync("v1", "Pod");

            // assert
            var containers = pod.ShouldNotBeNull()
                .Schema.ShouldNotBeNull()
                .GetPropertyElementType("spec").ShouldNotBeNull()
                .GetPropertyElementType("containers").ShouldNotBeNull();

            containers.MergeStrategy.ShouldBe(ElementMergeStrategy.MergeListOfObject);
            containers.MergeKey.ShouldBe("name");
        }

        [TestMethod]
        public async Task ArrayOfPrimativeWithoutExtensionsIsReplaceListOfPrimative()
        {
            // arrange
            var provider = SharedProvider;

            // act
            var pod = await provider.GetResourceKindAsync("v1", "Pod");

            // assert
            var args = pod.ShouldNotBeNull()
                .Schema.ShouldNotBeNull()
                .GetPropertyElementType("spec").ShouldNotBeNull()
                .GetPropertyElementType("containers").ShouldNotBeNull()
                .GetCollectionElementType().ShouldNotBeNull()
                .GetPropertyElementType("args");

            args.MergeStrategy.ShouldBe(ElementMergeStrategy.ReplaceListOfPrimative);
        }

        [TestMethod]
        public async Task ArrayOfPrimativeCanHaveMergeExtensions()
        {
            // arrange
            var provider = SharedProvider;

            // act
            var pod = await provider.GetResourceKindAsync("v1", "Pod");

            // assert
            var finalizers = pod.ShouldNotBeNull()
                .Schema.ShouldNotBeNull()
                .GetPropertyElementType("metadata").ShouldNotBeNull()
                .GetPropertyElementType("finalizers").ShouldNotBeNull();

            finalizers.MergeStrategy.ShouldBe(ElementMergeStrategy.MergeListOfPrimative);
        }

        [TestMethod]
        [DataRow("v2", "Pod")]
        [DataRow("v1", "FluxCapacitor")]
        public async Task NotFoundResourceKindComesProducesNull(string apiVersion, string kind)
        {
            // arrange
            var provider = SharedProvider;

            // act
            var resourceKind = await provider.GetResourceKindAsync(apiVersion, kind);

            // assert
            resourceKind.ShouldBeNull();
        }

        [TestMethod]
        public async Task DictionaryHasInformationAboutContents()
        {
            // arrange
            var provider = SharedProvider;

            // act
            var pod = await provider.GetResourceKindAsync("v1", "Pod");
            var labels = pod.Schema.GetPropertyElementType("metadata").GetPropertyElementType("labels");

            // assert
            labels.MergeStrategy.ShouldBe(ElementMergeStrategy.MergeMap);
            labels.GetCollectionElementType().MergeStrategy.ShouldBe(ElementMergeStrategy.ReplacePrimative);
        }
    }
}
