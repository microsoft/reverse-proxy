// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.ResourceKinds;
using Microsoft.Kubernetes.Resources.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Kubernetes.Resources;

public abstract partial class ResourcePatcherTestsBase
{
    public virtual IResourceKindManager Manager { get; set; }

    public async Task RunStandardTest(StandardTestYaml testYaml)
    {
        if (testYaml is null)
        {
            throw new ArgumentNullException(nameof(testYaml));
        }

        await RunThreeWayMerge(testYaml);
        if (!testYaml.Patch.Any(operation => operation.Op == "remove"))
        {
            await RunApplyLiveOnlyMerge(testYaml);
        }
    }

    private async Task RunThreeWayMerge(StandardTestYaml testYaml)
    {
        IResourcePatcher patcher = new ResourcePatcher();

        var parameters = new CreatePatchParameters
        {
            ApplyResource = testYaml.Apply,
            LastAppliedResource = testYaml.LastApplied,
            LiveResource = testYaml.Live,
        };

        if (testYaml.ResourceKind is not null)
        {
            parameters.ResourceKind = await Manager.GetResourceKindAsync(
                testYaml.ResourceKind.ApiVersion,
                testYaml.ResourceKind.Kind);
        }

        var patch = patcher.CreateJsonPatch(parameters);

        var operations = new ResourceSerializers().Convert<List<PatchOperation>>(patch);

        var expected = testYaml.Patch.OrderBy(op => op.ToString()).ToList();
        operations = operations.OrderBy(op => op.ToString()).ToList();
        Assert.Equal(expected, operations);
    }

    private async Task RunApplyLiveOnlyMerge(StandardTestYaml testYaml)
    {
        IResourcePatcher patcher = new ResourcePatcher();

        var parameters = new CreatePatchParameters
        {
            ApplyResource = testYaml.Apply,
            LiveResource = testYaml.Live,
        };

        if (testYaml.ResourceKind is not null)
        {
            parameters.ResourceKind = await Manager.GetResourceKindAsync(
                testYaml.ResourceKind.ApiVersion,
                testYaml.ResourceKind.Kind);
        }

        var patch = patcher.CreateJsonPatch(parameters);

        var operations = new ResourceSerializers().Convert<List<PatchOperation>>(patch);

        var expected = testYaml.Patch.OrderBy(op => op.ToString()).ToList();
        operations = operations.OrderBy(op => op.ToString()).ToList();
        Assert.Equal(expected, operations);
    }
}
