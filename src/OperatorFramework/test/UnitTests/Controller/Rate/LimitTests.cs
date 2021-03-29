// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;

namespace Microsoft.Kubernetes.Controller.Rate
{
    [TestClass]
    public class LimitTests
    {
        [TestMethod]
        [DataRow(15, 1, 15)]
        [DataRow(15, 120, 1800)]
        [DataRow(15, .1, 1.5)]
        [DataRow(300, 2, 600)]
        public void TokensFromDuration(double perSecond, double durationSeconds, double tokens)
        {
            // arrange
            var limit = new Limit(perSecond);

            // act
            var tokensFromDuration = limit.TokensFromDuration(TimeSpan.FromSeconds(durationSeconds));

            // assert
            tokensFromDuration.ShouldBe(tokens);
        }

        [TestMethod]
        [DataRow(15, 1, 15)]
        [DataRow(15, 120, 1800)]
        [DataRow(15, .1, 1.5)]
        [DataRow(300, 2, 600)]
        public void DurationFromTokens(double perSecond, double durationSeconds, double tokens)
        {
            // arrange
            var limit = new Limit(perSecond);

            // act
            var durationFromTokens = limit.DurationFromTokens(tokens);

            // assert
            durationFromTokens.ShouldBe(TimeSpan.FromSeconds(durationSeconds));
        }
    }
}
