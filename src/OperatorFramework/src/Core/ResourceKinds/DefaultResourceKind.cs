// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Kubernetes.ResourceKinds;

public sealed class DefaultResourceKind : IResourceKind
{
    public static IResourceKind Unknown { get; } = new DefaultResourceKind();

    public string ApiVersion => default;

    public string Kind => default;

    public IResourceKindElement Schema => DefaultResourceKindElement.Unknown;
}
