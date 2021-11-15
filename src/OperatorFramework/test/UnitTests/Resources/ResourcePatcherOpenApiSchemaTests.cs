// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.ResourceKinds;
using Microsoft.Kubernetes.ResourceKinds.OpenApi;
using Microsoft.Kubernetes.Resources.Models;
using Microsoft.Kubernetes.Utils;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Kubernetes.Resources;

public class ResourcePatcherOpenApiSchemaTests : ResourcePatcherTestsBase
{
    public static ResourceKindManager SharedManager { get; set; } = new(new[] { new OpenApiResourceKindProvider(new FakeLogger<OpenApiResourceKindProvider>()) });

    public ResourcePatcherOpenApiSchemaTests()
    {
        Manager = SharedManager;
    }

    [Fact]
    public async Task PrimativePropertiesCanBePatched()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task PrimativePropertiesCanAddedAndRemoved()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task DictionaryOfPrimativesCanBePatched()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task DictionaryOfPrimativesAddedAndRemoved()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task DictionaryOnlyRemoveIfWasLastApplied()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task ArrayOfPrimativesReplacedEntirelyWhenDifferent()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task MergedArrayOfPrimativesCanAddItems()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task MergedArrayOfPrimativesCanRemoveItemsIfLastApplied()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task MergedArrayOfPrimativesPreserveOrderOfAppliedValues()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task MergedArrayOfPrimativesPreserveItemsIfNotLastApplied()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task MergedArrayOfObjectsCanAddItems()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task MergedArrayOfObjectsCanRemoveItemsIfLastApplied()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task MergedArrayOfObjectsPreserveItemsIfNotLastApplied()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task MergedArrayOfObjectsPreserveOrderOfLiveValues()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task ArrayWithMergeKeyTreatedAsDictionary()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }
}
