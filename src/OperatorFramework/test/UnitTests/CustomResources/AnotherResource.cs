// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;

namespace Microsoft.Kubernetes.CustomResources;

[KubernetesEntity(ApiVersion = "another-version", Group = "another-group", Kind = "AnotherKind", PluralName = "anotherkinds")]
public class AnotherResource
{
}
