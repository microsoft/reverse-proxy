// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using IslandGateway.Utilities;

namespace IslandGateway.Core.Service
{
    internal sealed class MethodMatcher : RuleMatcherBase
    {
        public MethodMatcher(string name, string[] args)
            : base(name, args)
        {
            Contracts.Check(args.Length >= 1, $"Expected at least 1 argument, found {args.Length}.");
        }

        public string[] Methods => Args;
    }
}
