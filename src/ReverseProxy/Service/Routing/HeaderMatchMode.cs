// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.Routing
{
    /// <summary>
    /// How to compare header values.
    /// </summary>
    public enum HeaderMatchMode
    {
        /// <summary>
        /// Header value must match in its entirety, subject to the value of <see cref="IHeaderMetadata.CaseSensitive"/>.
        /// </summary>
        Exact,

        /// <summary>
        /// The header must exist, but any non-empty value is allowed.
        /// </summary>
        Exists,

        /// <summary>
        /// Header value must match by prefix, subject to the value of <see cref="IHeaderMetadata.CaseSensitive"/>.
        /// </summary>
        Prefix,
    }
}
