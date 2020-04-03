// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.Abstractions;

namespace Microsoft.ReverseProxy.Core.Configuration
{
    internal class ProxyConfigOptions
    {
        public IDictionary<string, Backend> Backends { get; } = new Dictionary<string, Backend>(StringComparer.Ordinal);
        public IList<ProxyRoute> Routes { get; } = new List<ProxyRoute>();
    }
}
