// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using IslandGateway.Core.Abstractions;

namespace IslandGateway.Core.Service
{

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
        Result<IList<RuleMatcherBase>, string> Parse(string rule);
    }
}
