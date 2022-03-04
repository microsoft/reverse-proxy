// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Resources.Models;
using Microsoft.Kubernetes.Utils;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Kubernetes.Resources;

public class ResourcePatcherUnknownSchemaTests : ResourcePatcherTestsBase
{
    [Fact]
    public async Task ObjectPropertyIsAddedWhenMissing()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task NestedPropertyIsAddedWhenMissing()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task TildaAndForwardSlashAreEscapedInPatchPaths()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task AdditionalPropertyIsAddedWhenMissing()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task PropertiesOfStringAreOnlyRemovedWhenPreviouslyAdded()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task PropertiesOfObjectAreOnlyRemovedWhenPreviouslyAdded()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task PropertiesOfNullAreOnlyRemovedWhenPreviouslyAdded()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task ArrayAreAddedAndRemovedEntirelyAsNeeded()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task ArraysAreReplacedEntirelyWhenDifferent()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task MergingWhenApplyElementTypeHasChanged()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }

    [Fact]
    public async Task MergingWhenLiveElementTypeHasChanged()
    {
        await RunStandardTest(TestYaml.LoadFromEmbeddedStream<StandardTestYaml>());
    }
}
