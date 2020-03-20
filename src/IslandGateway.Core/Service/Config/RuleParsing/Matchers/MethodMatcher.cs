// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using IslandGateway.Utilities;

namespace IslandGateway.Core.Service
{
    internal sealed class MethodMatcher : RuleMatcherBase
    {
        public MethodMatcher(string[] args)
            : base("Method")
        {
            Contracts.Check(args.Length >= 1, $"Expected at least 1 argument, found {args.Length}.");

            Methods = args;
        }

        public string[] Methods { get; }

        public override string ToString()
        {
            return $"{Name}({string.Join(", ", Methods)})";
        }

        public override bool Equals(object obj)
        {
            return obj is MethodMatcher other && Methods.SequenceEqual(other.Methods, StringComparer.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }
}
