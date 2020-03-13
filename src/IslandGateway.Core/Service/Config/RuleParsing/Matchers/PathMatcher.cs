// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using IslandGateway.Utilities;

namespace IslandGateway.Core.Service
{
    internal sealed class PathMatcher : RuleMatcherBase
    {
        public PathMatcher(string name, string[] args)
            : base(name, args)
        {
            Contracts.Check(args.Length == 1, $"Expected 1 argument, found {args.Length}.");
            Contracts.CheckNonEmpty(args[0], $"{nameof(args)}[0]");
        }

        public string Pattern => Args[0];
    }
}
