// <copyright file="RuleMatcherBase.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Linq;
using IslandGateway.Utilities;

namespace IslandGateway.Core.Service
{
    internal abstract class RuleMatcherBase
    {
        protected RuleMatcherBase(string name, string[] args)
        {
            Contracts.CheckNonEmpty(name, nameof(name));
            Contracts.CheckValue(args, nameof(args));
            Name = name;
            Args = args;
        }

        internal string Name { get; }
        internal string[] Args { get; }

        public override string ToString()
        {
            return $"{Name}({FormatArgs()})";

            string FormatArgs()
            {
                return string.Join(", ", Args.Select(FormatArg));

                static string FormatArg(string arg)
                {
                    return $"'{arg.Replace("'", "''")}'";
                }
            }
        }
    }
}
