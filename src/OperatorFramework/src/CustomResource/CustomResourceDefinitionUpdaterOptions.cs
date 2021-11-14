// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Kubernetes.CustomResources;

public class CustomResourceDefinitionUpdaterOptions<TResource> : CustomResourceDefinitionUpdaterOptions
{
}

public class CustomResourceDefinitionUpdaterOptions
{
    public string Scope { get; set; }
}
