// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using IslandGateway.Utilities;

namespace IslandGateway.Core.Service
{
    internal sealed class PathMatcher : RuleMatcherBase
    {
        public PathMatcher(string arg)
            : base("Path")
        {
            Contracts.CheckNonEmpty(arg, $"{nameof(arg)}");
            Pattern = arg;
        }

        public string Pattern { get; }

        public override string ToString()
        {
            return $"{Name}({Pattern})";
        }

        public override bool Equals(object obj)
        {
            return obj is PathMatcher other && string.Equals(Pattern, other.Pattern, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return Pattern.GetHashCode();
        }
    }
}
