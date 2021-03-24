// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Kubernetes.ResourceKinds
{
    public interface IResourceKindElement
    {
        ElementMergeStrategy MergeStrategy { get; }

        public string MergeKey { get; }

        IResourceKindElement GetPropertyElementType(string name);

        IResourceKindElement GetCollectionElementType();
    }
}
