// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using IslandGateway.Core.Service;

namespace IslandGateway.Core.ConfigModel
{
    internal class ParsedRoute
    {
        /// <summary>
        /// Unique identifier of this route.
        /// </summary>
        public string RouteId { get; set; }

        /// <summary>
        /// Gets or sets the parsed matchers for this route. This is computed
        /// from the original route's rule.
        /// </summary>
        public IList<RuleMatcherBase> Matchers { get; set; }

        /// <summary>
        /// Gets or sets the priority of this route.
        /// Routes with higher priority are evaluated first.
        /// </summary>
        public int? Priority { get; set; }

        /// <summary>
        /// Gets or sets the backend that requests matching this route
        /// should be proxied to.
        /// </summary>
        public string BackendId { get; set; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this route.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }
    }
}
