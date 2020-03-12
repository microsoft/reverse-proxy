// <copyright file="IRuleParser.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;
using IslandGateway.Core.Abstractions;

namespace IslandGateway.Core.Service
{
#pragma warning disable SA1200 // Using directives should be placed correctly
    using RuleParseResult = Result<IList<RuleMatcherBase>, string>;
#pragma warning restore SA1200 // Using directives should be placed correctly

    /// <summary>
    /// Interface for a class that parses Core Gateway rules
    /// such as <c>HostName('abc.example.com') &amp;&amp; PathPrefix('/a/b')</c>
    /// and produces the corresponding AST.
    /// </summary>
    internal interface IRuleParser
    {
        /// <summary>
        /// Parses a rule and produces the corresponding AST or an error value.
        /// </summary>
        RuleParseResult Parse(string rule);
    }
}