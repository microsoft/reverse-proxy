// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Configuration
{
    /// <summary>
    /// How to match Query Parameter values.
    /// </summary>
    public enum QueryParameterMatchMode
    {
        /// <summary>
        /// The query parameter must match in its entirety,
        /// Subject to case sensitivity settings.
        /// Only single query parameter name supported. If there are multiple query parameters with the same name then the match fails.
        /// </summary>
        Exact,

        /// <summary>
        /// ANY one of the values for the query parameter key must be present. Key must have a value matched.
        /// Subject to case sensitivity settings.
        /// Only single query parameter name supported. If there are multiple query parameters with the same name then the match fails.
        /// </summary>
        AnyValues,

        /// <summary>
        /// Query string key must be present and substring must match for each of the respective query string values.
        /// Subject to case sensitivity settings.
        /// Only single query parameter name supported. If there are multiple query parameters with the same name then the match fails.
        /// </summary>
        Contains,

        /// <summary>
        /// ALL of the values of the query parameter key must NOT be present.
        /// Subject to case sensitivity settings.
        /// Only single query parameter name supported. If there are multiple query parameters with the same name then the match fails.
        /// </summary>
        NotContains,

        /// <summary>
        /// Query key must be present. ANY one of the values for the query key must be present as substring.
        /// Subject to case sensitivity settings.
        /// Only single query parameter name supported. If there are multiple query parameters with the same name then the match fails.
        /// </summary>
        AnyContains,

        /// <summary>
        /// The header must exist and contain any non-empty value.
        /// </summary>
        Exists
    }
}
