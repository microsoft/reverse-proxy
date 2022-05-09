// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Kubernetes.ResourceKinds;

public sealed class DefaultResourceKindElement : IResourceKindElement
{
    public DefaultResourceKindElement(ElementMergeStrategy replacePrimative)
    {
        MergeStrategy = replacePrimative;
    }

    public static IResourceKindElement Unknown { get; } = new DefaultResourceKindElement(ElementMergeStrategy.Unknown);

    public static IResourceKindElement ReplacePrimative { get; } = new DefaultResourceKindElement(ElementMergeStrategy.ReplacePrimative);

    public ElementMergeStrategy MergeStrategy { get; }

    public string MergeKey => string.Empty;

    public IResourceKindElement GetPropertyElementType(string name) => Unknown;

    public IResourceKindElement GetCollectionElementType() => Unknown;
}
