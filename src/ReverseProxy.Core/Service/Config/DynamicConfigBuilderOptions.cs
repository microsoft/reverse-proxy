// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.Abstractions;

namespace Microsoft.ReverseProxy.Core.Service
{
    internal class DynamicConfigBuilderOptions
    {
        public IList<Action<ProxyRoute>> RouteDefaultConfigs { get; } = new List<Action<ProxyRoute>>();

        public IDictionary<string, IList<Action<ProxyRoute>>> RouteConfigs { get; } = new Dictionary<string, IList<Action<ProxyRoute>>>();

        public IList<Action<string, Backend>> BackendDefaultConfigs { get; } = new List<Action<string, Backend>>();

        public IDictionary<string, IList<Action<Backend>>> BackendConfigs { get; } = new Dictionary<string, IList<Action<Backend>>>();
    }
}
