// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Collections.Generic;

namespace Microsoft.Kubernetes.Controller.Queues
{
    [TestClass]
    public class RateLimitingQueueTests
    {
        [TestMethod]
        public void AddRateLimitedCallsWhenAndAddDelay()
        {
            // arrange
            var whenResults = new Dictionary<string, TimeSpan>
            {
                { "one", TimeSpan.FromMilliseconds(15) },
                { "two", TimeSpan.FromMilliseconds(0) },
                { "three", TimeSpan.FromMilliseconds(30) },
           };
            var whenCalls = new List<string>();
            var rateLimiter = new FakeRateLimiter<string>
            {
                OnItemDelay = item =>
                {
                    whenCalls.Add(item);
                    return whenResults[item];
                },
            };
            var addAfterCalls = new List<(string item, TimeSpan delay)>();
            var @base = new FakeQueue<string>
            {
                OnAddAfter = (item, delay) => addAfterCalls.Add((item, delay)),
            };
            var queue = new RateLimitingQueue<string>(rateLimiter, @base);

            // act
            queue.AddRateLimited("one");
            queue.AddRateLimited("two");
            queue.AddRateLimited("three");

            // assert
            whenCalls.ShouldBe(new[]
            {
                "one",
                "two",
                "three"
            });
            addAfterCalls.ShouldBe(new[]
            {
                ("one", TimeSpan.FromMilliseconds(15)),
                ("two", TimeSpan.FromMilliseconds(0)),
                ("three", TimeSpan.FromMilliseconds(30)),
            });
        }
    }
}
