// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using IslandGateway.Core.Abstractions;
using IslandGateway.Utilities;
using Superpower;
using Superpower.Display;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace IslandGateway.Core.Service
{
    /// <summary>
    /// Interface for a class that parses Core Gateway rules
    /// such as <c>HostName('abc.example.com') &amp;&amp; PathPrefix('/a/b')</c>
    /// and produces the corresponding AST.
    /// </summary>
    internal class RuleParser : IRuleParser
    {
        internal enum RuleToken
        {
            None,

            [Token(Category = "identifier", Example = "Host, PathPrefix, Path, ...")]
            Identifier,

            [Token(Category = "parentheses", Example = "(")]
            LParen,

            [Token(Category = "parentheses", Example = ")")]
            RParen,

            [Token(Category = "string literal", Example = "'example ''with'' quotes'")]
            StringLiteral,

            [Token(Category = "comma", Example = ",")]
            Comma,

            [Token(Category = "operator", Example = "&&")]
            LogicalAnd,

            [Token(Category = "operator", Example = "||")]
            LogicalOr,
        }

        public Result<IList<RuleMatcherBase>, string> Parse(string rule)
        {
            Contracts.CheckValue(rule, nameof(rule));

            MatcherNode[] parsedNodes;
            try
            {
                var tokens = Lexer.Tokenize(rule);
                parsedNodes = Parser.Parse(tokens);
            }
            catch (ParseException ex)
            {
                return Result<IList<RuleMatcherBase>, string>.Failure($"Parse error: {ex.Message}");
            }

            var results = new List<RuleMatcherBase>(parsedNodes.Length);
            foreach (var node in parsedNodes)
            {
                RuleMatcherBase matcher;
                switch (node.MatcherName.ToUpperInvariant())
                {
                    case "HOST":
                        if (node.Arguments.Length != 1)
                        {
                            return Result<IList<RuleMatcherBase>, string>.Failure($"'Host' matcher requires one argument, found {node.Arguments.Length}");
                        }
                        matcher = new HostMatcher("Host", node.Arguments);
                        break;
                    case "PATH":
                        if (node.Arguments.Length != 1)
                        {
                            return Result<IList<RuleMatcherBase>, string>.Failure($"'Path' matcher requires one argument, found {node.Arguments.Length}");
                        }
                        matcher = new PathMatcher("Path", node.Arguments);
                        break;
                    case "METHOD":
                        if (node.Arguments.Length < 1)
                        {
                            return Result<IList<RuleMatcherBase>, string>.Failure($"'Method' matcher requires at least one argument, found {node.Arguments.Length}");
                        }
                        matcher = new MethodMatcher("Method", node.Arguments);
                        break;
                    case "QUERY":
                    case "HEADER":
                    default:
                        return Result<IList<RuleMatcherBase>, string>.Failure($"Unsupported matcher '{node.MatcherName}'");
                }

                results.Add(matcher);
            }

            return Result<IList<RuleMatcherBase>, string>.Success(results);
        }

        internal static class Lexer
        {
            private static readonly Tokenizer<RuleToken> Tokenizer = new TokenizerBuilder<RuleToken>()
                .Ignore(Span.WhiteSpace)
                .Match(Character.EqualTo('('), RuleToken.LParen)
                .Match(Character.EqualTo(')'), RuleToken.RParen)
                .Match(Character.EqualTo(','), RuleToken.Comma)
                .Match(Span.EqualTo("&&"), RuleToken.LogicalAnd)
                .Match(Span.EqualTo("||"), RuleToken.LogicalOr)
                .Match(Identifier.CStyle, RuleToken.Identifier)
                .Match(QuotedString.SqlStyle, RuleToken.StringLiteral)
                .Build();

            public static TokenList<RuleToken> Tokenize(string input)
            {
                return Tokenizer.Tokenize(input);
            }
        }

        internal static class Parser
        {
            private static readonly TokenListParser<RuleToken, string> SingleArgument =
                from stringLiteral in Token.EqualTo(RuleToken.StringLiteral).Apply(QuotedString.SqlStyle)
                select stringLiteral;

            private static readonly TokenListParser<RuleToken, MatcherNode> MatcherInvocation =
                from identifier in Token.EqualTo(RuleToken.Identifier)
                from open in Token.EqualTo(RuleToken.LParen)
                from arg in SingleArgument.ManyDelimitedBy(Token.EqualTo(RuleToken.Comma))
                from close in Token.EqualTo(RuleToken.RParen)
                select new MatcherNode { MatcherName = identifier.ToStringValue(), Arguments = arg };

            private static readonly TokenListParser<RuleToken, MatcherNode[]> MatcherInvocations =
                from matchers in MatcherInvocation.ManyDelimitedBy(Token.EqualTo(RuleToken.LogicalAnd)).AtEnd()
                select matchers;

            public static MatcherNode[] Parse(TokenList<RuleToken> tokens)
            {
                return MatcherInvocations.Parse(tokens);
            }
        }

        internal class MatcherNode
        {
            public string MatcherName { get; set; }
            public string[] Arguments { get; set; }
        }
    }
}
