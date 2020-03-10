// <copyright file="PathMatcher.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.CoreServicesBorrowed;

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

        public string Pattern => this.Args[0];
    }
}