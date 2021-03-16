// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.ResourceKinds;
using Microsoft.Kubernetes.ResourceKinds.OpenApi;
using Microsoft.Kubernetes.Resources.Models;
using Microsoft.Kubernetes.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Resources
{
    [TestClass]
    public class ResourcePatcherOpenApiSchemaTests : ResourcePatcherTestsBase
    {
        public static ResourceKindManager SharedManager { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            if (testContext is null)
            {
                throw new System.ArgumentNullException(nameof(testContext));
            }

            SharedManager = new ResourceKindManager(new[] { new OpenApiResourceKindProvider(new FakeLogger<OpenApiResourceKindProvider>()) });
        }

        [TestInitialize]
        public void TestInitialize()
        {
            Manager = SharedManager;
        }

        [TestMethod]
        public async Task PrimativePropertiesCanBePatched()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task PrimativePropertiesCanAddedAndRemoved()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task DictionaryOfPrimativesCanBePatched()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task DictionaryOfPrimativesAddedAndRemoved()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task DictionaryOnlyRemoveIfWasLastApplied()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task ArrayOfPrimativesReplacedEntirelyWhenDifferent()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task MergedArrayOfPrimativesCanAddItems()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task MergedArrayOfPrimativesCanRemoveItemsIfLastApplied()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task MergedArrayOfPrimativesPreserveOrderOfAppliedValues()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task MergedArrayOfPrimativesPreserveItemsIfNotLastApplied()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task MergedArrayOfObjectsCanAddItems()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task MergedArrayOfObjectsCanRemoveItemsIfLastApplied()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task MergedArrayOfObjectsPreserveItemsIfNotLastApplied()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task MergedArrayOfObjectsPreserveOrderOfLiveValues()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task ArrayWithMergeKeyTreatedAsDictionary()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }
    }
}
