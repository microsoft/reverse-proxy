// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.ResourceKinds;

namespace Microsoft.Kubernetes.Resources;

public class CreatePatchParameters
{
    public IResourceKind ResourceKind { get; set; }
    public object ApplyResource { get; set; }
    public object LastAppliedResource { get; set; }
    public object LiveResource { get; set; }
}
