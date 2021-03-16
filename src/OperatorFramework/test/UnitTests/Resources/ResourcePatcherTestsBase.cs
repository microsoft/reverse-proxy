// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.ResourceKinds;
using Microsoft.Kubernetes.Resources.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Resources
{
    [TestClass]
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
            // arrange
            IResourcePatcher patcher = new ResourcePatcher();

            // act
            var parameters = new CreatePatchParameters
            {
                ApplyResource = testYaml.Apply,
                LastAppliedResource = testYaml.LastApplied,
                LiveResource = testYaml.Live,
            };

            if (testYaml.ResourceKind != null)
            {
                parameters.ResourceKind = await Manager.GetResourceKindAsync(
                    testYaml.ResourceKind.ApiVersion,
                    testYaml.ResourceKind.Kind);
            }

            var patch = patcher.CreateJsonPatch(parameters);

            // assert
            var operations = new ResourceSerializers().Convert<PatchOperation[]>(patch);
            operations.ShouldBe(testYaml.Patch, ignoreOrder: true);
        }

        private async Task RunApplyLiveOnlyMerge(StandardTestYaml testYaml)
        {
            // arrange
            IResourcePatcher patcher = new ResourcePatcher();

            // act
            var parameters = new CreatePatchParameters
            {
                ApplyResource = testYaml.Apply,
                LiveResource = testYaml.Live,
            };

            if (testYaml.ResourceKind != null)
            {
                parameters.ResourceKind = await Manager.GetResourceKindAsync(
                    testYaml.ResourceKind.ApiVersion,
                    testYaml.ResourceKind.Kind);
            }

            var patch = patcher.CreateJsonPatch(parameters);

            // assert
            var operations = new ResourceSerializers().Convert<List<PatchOperation>>(patch);
            operations.ShouldBe(testYaml.Patch, ignoreOrder: true);
        }
    }
}
