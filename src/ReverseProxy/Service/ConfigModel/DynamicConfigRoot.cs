// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.ConfigModel
{
    internal class DynamicConfigRoot
    {
        public IDictionary<string, Backend> Backends { get; set; }
        public IList<ParsedRoute> Routes { get; set; }
    }
}
