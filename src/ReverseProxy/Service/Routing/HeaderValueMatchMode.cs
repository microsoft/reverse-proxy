// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.Routing
{
    /// <summary>
    /// How to compare header values.
    /// </summary>
    public enum HeaderValueMatchMode
    {
        /// <summary>
        /// Header value must match in its entirety, subject to the value of <see cref="IHeaderMetadata.ValueIgnoresCase"/>.
        /// </summary>
        Exact,

        /// <summary>
        /// Header value must match by prefix, subject to the value of <see cref="IHeaderMetadata.ValueIgnoresCase"/>.
        /// </summary>
        Prefix,
    }
}
