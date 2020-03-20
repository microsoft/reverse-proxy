// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using IslandGateway.Utilities;

namespace IslandGateway.Core.Service
{
    internal sealed class HostMatcher : RuleMatcherBase
    {
        public HostMatcher(string arg)
            : base("Host")
        {
            Contracts.CheckNonEmpty(arg, $"{nameof(arg)}");
            Host = arg;
        }

        public string Host { get; }

        public override string ToString()
        {
            return $"{Name}({Host})";
        }

        public override bool Equals(object obj)
        {
            return obj is HostMatcher other && string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return Host.GetHashCode();
        }
    }
}
