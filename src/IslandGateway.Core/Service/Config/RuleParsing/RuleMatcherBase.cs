// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using IslandGateway.Utilities;

namespace IslandGateway.Core.Service
{
    internal abstract class RuleMatcherBase
    {
        protected RuleMatcherBase(string name)
        {
            Contracts.CheckNonEmpty(name, nameof(name));
            Name = name;
        }

        internal string Name { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
