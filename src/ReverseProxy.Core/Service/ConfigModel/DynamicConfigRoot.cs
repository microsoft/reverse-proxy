// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Core.ConfigModel
{
    internal class DynamicConfigRoot
    {
        public IList<BackendWithEndpoints> Backends { get; set; }
        public IList<ParsedRoute> Routes { get; set; }
    }
}
