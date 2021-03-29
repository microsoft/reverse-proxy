// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Resources.Models;
using Microsoft.Kubernetes.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Resources
{
    [TestClass]
    public class ResourcePatcherUnknownSchemaTests : ResourcePatcherTestsBase
    {
        [TestMethod]
        public async Task ObjectPropertyIsAddedWhenMissing()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task NestedPropertyIsAddedWhenMissing()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task TildaAndForwardSlashAreEscapedInPatchPaths()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task AdditionalPropertyIsAddedWhenMissing()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task PropertiesOfStringAreOnlyRemovedWhenPreviouslyAdded()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task PropertiesOfObjectAreOnlyRemovedWhenPreviouslyAdded()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task PropertiesOfNullAreOnlyRemovedWhenPreviouslyAdded()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task ArrayAreAddedAndRemovedEntirelyAsNeeded()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task ArraysAreReplacedEntirelyWhenDifferent()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task MergingWhenApplyElementTypeHasChanged()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }

        [TestMethod]
        public async Task MergingWhenLiveElementTypeHasChanged()
        {
            await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
        }
    }
}
