// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace IslandGateway.Core.ConfigModel
{
    internal class DynamicConfigRoot
    {
        public IList<BackendWithEndpoints> Backends { get; set; }
        public IList<ParsedRoute> Routes { get; set; }
    }
}
