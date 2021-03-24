// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Kubernetes.ResourceKinds
{
    public enum ElementMergeStrategy
    {
        /// <summary>
        /// Unknown json schema are handled by MergeObject, ReplacePrimative, and ReplaceListOfPrimative.
        /// </summary>
        Unknown,

        /// <summary>
        /// Updating object by matching property names.
        /// </summary>
        MergeObject,

        /// <summary>
        /// Updating primative by replacing when different.
        /// </summary>
        ReplacePrimative,

        /// <summary>
        /// Updating dictionary by matching keys.
        /// </summary>
        MergeMap,

        /// <summary>
        /// Merging list or primatives adding or removing when changed but leaving unmanaged values alone.
        /// </summary>
        MergeListOfPrimative,

        /// <summary>
        /// Updating list of primatives by replacing entirely when differences exist.
        /// </summary>
        ReplaceListOfPrimative,

        /// <summary>
        /// Merging list of objects when the "merge key" property for object matching is known.
        /// </summary>
        MergeListOfObject,

        /// <summary>
        /// Updating list of object by replacing entirely when differences exist.
        /// </summary>
        ReplaceListOfObject,
    }
}
