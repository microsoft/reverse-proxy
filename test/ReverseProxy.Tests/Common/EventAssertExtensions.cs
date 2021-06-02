// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Xunit;
using Yarp.ReverseProxy.Proxy;

namespace Yarp.ReverseProxy.Common.Tests
{
    internal static class EventAssertExtensions
    {
        public static (ProxyStage Stage, DateTime TimeStamp)[] GetProxyStages(this List<EventWrittenEventArgs> events)
        {
            return events
                .Where(e => e.EventName == "ProxyStage")
                .Select(e =>
                {
                    var stage = (ProxyStage)Assert.Single(e.Payload);
                    Assert.InRange(stage, ProxyStage.SendAsyncStart, ProxyStage.ResponseUpgrade);
                    return (stage, e.TimeStamp);
                })
                .ToArray();
        }

        public static void AssertContainProxyStages(this List<EventWrittenEventArgs> events, bool hasRequestContent = true, bool upgrade = false, bool hasResponseContent = true)
        {
            var stages = new List<ProxyStage>()
            {
                ProxyStage.SendAsyncStart,
                ProxyStage.SendAsyncStop,
            };

            if (hasRequestContent)
            {
                stages.Add(ProxyStage.RequestContentTransferStart);
            }

            if (upgrade)
            {
                stages.Add(ProxyStage.ResponseUpgrade);
            }

            if (hasResponseContent)
            {
                stages.Add(ProxyStage.ResponseContentTransferStart);
            }

            events.AssertContainProxyStages(stages.ToArray());
        }

        public static void AssertContainProxyStages(this List<EventWrittenEventArgs> events, ProxyStage[] expectedStages)
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
