// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Configuration
{
    /// <summary>
    /// How to match header values.
    /// </summary>
    public enum QueryParameterMatchMode
    {
        /// <summary>
        /// The query parameter must match in its entirety,
        /// subject to case sensitivity settings.
        /// Only single query parameter are supported. If there are multiple query parameters with the same name then the match fails.
        /// </summary>
        Exact,

        /// <summary>
        /// ANY one of the values for each of the query parameter keys must be present. All keys must have a value matched.
        /// subject to case sensitivity settings.
        /// Only single headers are supported. If there are multiple headers with the same name then the match fails.
        /// </summary>
        AnyValues,

        /// <summary>
        /// ALL query string keys must be present and substring must match for each of the respective query string values.
        /// subject to case sensitivity settings.
        /// Only single headers are supported. If there are multiple headers with the same name then the match fails.
        /// </summary>
        Contains,

        /// <summary>
        /// ALL of the values for each of the query parameter keys must NOT be present.
        /// subject to case sensitivity settings.
        /// Only single headers are supported. If there are multiple headers with the same name then the match fails.
        /// </summary>
        NotContains,

        /// <summary>
        /// ALL of the query keys must be present. ANY one of the values for each of the query key must be present as substring.
        /// subject to case sensitivity settings.
        /// Only single headers are supported. If there are multiple headers with the same name then the match fails.
        /// </summary>
        AnyContains,
    }
}
