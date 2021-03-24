// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.JsonPatch;

namespace Microsoft.Kubernetes.Resources
{
    public interface IResourcePatcher
    {
        JsonPatchDocument CreateJsonPatch(CreatePatchParameters parameters);
    }
}
