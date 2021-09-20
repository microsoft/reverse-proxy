// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.Common.Tests
{
    internal static class EventAssertExtensions
    {
        public static (ForwarderStage Stage, DateTime TimeStamp)[] GetProxyStages(this List<EventWrittenEventArgs> events)
        {
            return events
                .Where(e => e.EventName == "ForwarderStage")
                .Select(e =>
                {
                    var stage = (ForwarderStage)Assert.Single(e.Payload);
                    Assert.InRange(stage, ForwarderStage.SendAsyncStart, ForwarderStage.ResponseUpgrade);
                    return (stage, e.TimeStamp);
                })
                .ToArray();
        }

        public static void AssertContainProxyStages(this List<EventWrittenEventArgs> events, bool hasRequestContent = true, bool upgrade = false, bool hasResponseContent = true)
        {
            var stages = new List<ForwarderStage>()
            {
                ForwarderStage.SendAsyncStart,
                ForwarderStage.SendAsyncStop,
            };

            if (hasRequestContent)
            {
                stages.Add(ForwarderStage.RequestContentTransferStart);
            }

            if (upgrade)
            {
                stages.Add(ForwarderStage.ResponseUpgrade);
            }

            if (hasResponseContent)
            {
                stages.Add(ForwarderStage.ResponseContentTransferStart);
            }

            events.AssertContainProxyStages(stages.ToArray());
        }

        public static void AssertContainProxyStages(this List<EventWrittenEventArgs> events, ForwarderStage[] expectedStages)
        {
            var proxyStages = events.GetProxyStages()
                .Select(s => s.Stage)
                .ToArray();

            var presentStages = proxyStages.ToHashSet();

            Assert.Equal(presentStages.Count, proxyStages.Length);

            foreach (var expectedStage in expectedStages)
            {
                Assert.Contains(expectedStage, presentStages);
            }

            presentStages.RemoveWhere(s => expectedStages.Contains(s));

            Assert.Empty(presentStages);
        }
    }
}
