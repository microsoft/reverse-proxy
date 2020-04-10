// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.Abstractions;

namespace Microsoft.ReverseProxy.Core.ConfigModel
{
    internal class DynamicConfigRoot
    {
        public IDictionary<string, Backend> Backends { get; set; }
        public IList<ParsedRoute> Routes { get; set; }
    }
}
