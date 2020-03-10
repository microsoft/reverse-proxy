// <copyright file="RuleParserTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using FluentAssertions;
using IslandGateway.Core.Abstractions;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.Tests
{
    public class RuleParserTests : TestAutoMockBase
    {
        [Fact]
        public void Constructor_Works()
        {
            this.Create<RuleParser>();
        }

        [Fact]
        public void NullInput_ThrowsArgumentNullException()
        {
            // Arrange
            var parser = this.Create<RuleParser>();

            // Act & Assert
            Action action = () => parser.Parse(null);
            action.Should().ThrowExactly<ArgumentNullException>();
        }

        [Theory]
        [InlineData("Host('example.com')")]
        [InlineData("Host('example.com')    ", "Host('example.com')")]
        [InlineData("    Host('example.com')", "Host('example.com')")]
        [InlineData("Host (  'example.com' )", "Host('example.com')")]
        [InlineData("Host('example.com') && Path('/a')")]
        [InlineData("Host('example.com') && Method('get')")]
        [InlineData("Host('example.com') && Method('get', 'post')")]
        [InlineData("Host('example.com')&&Path('/a') ", "Host('example.com') && Path('/a')")]
        [InlineData("Host('*.example.com') && Path('/a''')")]
        public void ValidRule_Works(string input, string expectedOutputIfDifferent = null)
        {
            // Arrange
            var parser = this.Create<RuleParser>();

            // Act
            var output = parser.Parse(input);

            // Assert
            output.IsSuccess.Should().BeTrue();
            Serialize(output).Should().Be(expectedOutputIfDifferent ?? input);
        }

        [Theory]
        [InlineData("Host(\"example.com\")", "Parse error: Syntax error (line 1, column 6): unexpected `\"`.")]
        [InlineData("Host(@'example.com')", "Parse error: Syntax error (line 1, column 6): unexpected `@`.")]
        [InlineData("&& Host('example.com')", "Parse error: Syntax error (line 1, column 1): unexpected operator `&&`.")]
        [InlineData("Host('example.com')&&", "Parse error: Syntax error: unexpected end of input, expected `Host, PathPrefix, Path, ...`.")]
        [InlineData("Host(a)", "Parse error: Syntax error (line 1, column 6): unexpected identifier `a`, expected `)`.")]
        [InlineData("Host(example.com)", "Parse error: Syntax error (line 1, column 13): unexpected `.`.")]
        [InlineData("Host('example.com' && PathPrefix('/a'))", "Parse error: Syntax error (line 1, column 20): unexpected operator `&&`, expected `)`.")]
        public void InvalidRuleSyntax_ProducesGoodErrorMessage(string input, string expectedOutput)
        {
            // Arrange
            var parser = this.Create<RuleParser>();

            // Act
            var output = parser.Parse(input);

            // Assert
            output.IsSuccess.Should().BeFalse();
            Serialize(output).Should().Be(expectedOutput ?? input);
        }

        [Theory]
        [InlineData("Host()", "'Host' matcher requires one argument, found 0")]
        [InlineData("Path()", "'Path' matcher requires one argument, found 0")]
        [InlineData("Method()", "'Method' matcher requires at least one argument, found 0")]
        [InlineData("Host('a','b')", "'Host' matcher requires one argument, found 2")]
        [InlineData("Path('a','b')", "'Path' matcher requires one argument, found 2")]
        [InlineData("Host('a','b', 'c' )", "'Host' matcher requires one argument, found 3")]
        public void InvalidRuleSemantics_ProducesGoodErrorMessage(string input, string expectedOutput)
        {
            // Arrange
            var parser = this.Create<RuleParser>();

            // Act
            var output = parser.Parse(input);

            // Assert
            output.IsSuccess.Should().BeFalse();
            Serialize(output).Should().Be(expectedOutput ?? input);
        }

        private static string Serialize(Result<IList<RuleMatcherBase>, string> parsed)
        {
            if (!parsed.IsSuccess)
            {
                return parsed.Error;
            }

            return string.Join(" && ", parsed.Value);
        }
    }
}
