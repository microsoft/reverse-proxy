// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;

namespace Microsoft.Kubernetes.CustomResources
{
    [KubernetesEntity(ApiVersion = "test-version", Group = "test-group", Kind = "TestKind", PluralName = "testkinds")]
    public class SimpleResource
    {
    }
}
