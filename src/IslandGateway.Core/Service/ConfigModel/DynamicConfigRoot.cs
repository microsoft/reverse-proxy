// <copyright file="DynamicConfigRoot.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace IslandGateway.Core.ConfigModel
{
    internal class DynamicConfigRoot
    {
        public IList<BackendWithEndpoints> Backends { get; set; }
        public IList<ParsedRoute> Routes { get; set; }
    }
}
