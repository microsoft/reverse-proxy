// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Fakes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Kubernetes.Controller.Queues
{
    public class DelayingQueueTests
    {
        [Fact]
        public void DelayingQueuePassesCallsThrough()
        {
            var added = new List<string>();
            var doned = new List<string>();
            var fake = new FakeQueue<string>
            {
                OnAdd = added.Add,
                OnDone = doned.Add,
                OnLen = () => 42,
            };
            var clock = new FakeSystemClock();
            IDelayingQueue<string> delayingQueue = new DelayingQueue<string>(clock, fake);

            delayingQueue.Add("one");
            delayingQueue.Done("two");
            var len = delayingQueue.Len();

            Assert.Equal("one", Assert.Single(added));
            Assert.Equal("two", Assert.Single(doned));
            Assert.Equal(42, len);
        }

        [Fact(Skip = "https://github.com/microsoft/reverse-proxy/issues/1357")]
        public async Task DelayingQueueAddsWhenTimePasses()
        {
            var added = new List<string>();
            var fake = new FakeQueue<string>
            {
                OnAdd = added.Add,
            };
            var clock = new FakeSystemClock();
            IDelayingQueue<string> delayingQueue = new DelayingQueue<string>(clock, fake);

            delayingQueue.AddAfter("50ms", TimeSpan.FromMilliseconds(50));
            delayingQueue.AddAfter("100ms", TimeSpan.FromMilliseconds(100));
            clock.Advance(TimeSpan.FromMilliseconds(25));
            delayingQueue.AddAfter("75ms", TimeSpan.FromMilliseconds(50));
            delayingQueue.AddAfter("125ms", TimeSpan.FromMilliseconds(100));

            await Task.Delay(TimeSpan.FromMilliseconds(40));
            var countAfter25ms = added.Count;
            clock.Advance(TimeSpan.FromMilliseconds(30));
            await Task.Delay(TimeSpan.FromMilliseconds(40));
            var countAfter55ms = added.Count;
            clock.Advance(TimeSpan.FromMilliseconds(25));
            await Task.Delay(TimeSpan.FromMilliseconds(40));
            var countAfter80ms = added.Count;
            clock.Advance(TimeSpan.FromMilliseconds(25));
            await Task.Delay(TimeSpan.FromMilliseconds(40));
            var countAfter105ms = added.Count;
            clock.Advance(TimeSpan.FromMilliseconds(25));
            await Task.Delay(TimeSpan.FromMilliseconds(40));
            var countAfter135ms = added.Count;

            Assert.Equal(0, countAfter25ms);
            Assert.Equal(1, countAfter55ms);
            Assert.Equal(2, countAfter80ms);
            Assert.Equal(3, countAfter105ms);
            Assert.Equal(4, countAfter135ms);
            Assert.Equal(new[] { "50ms", "75ms", "100ms", "125ms" }, added);
        }

        [Fact]
        public async Task ZeroDelayAddsInline()
        {
            var state = "setup";
            var added = new List<(string state, string item)>();
            var fake = new FakeQueue<string>
            {
                OnAdd = item => added.Add((state, item)),
            };
            var clock = new FakeSystemClock();
            IDelayingQueue<string> delayingQueue = new DelayingQueue<string>(clock, fake);

            state = "before-one";
            delayingQueue.AddAfter("one", TimeSpan.FromMilliseconds(1));
            state = "after-one";
            await Task.Delay(TimeSpan.FromMilliseconds(40));

            state = "before-two";
            delayingQueue.AddAfter("two", TimeSpan.FromMilliseconds(0));
            state = "after-two";
            await Task.Delay(TimeSpan.FromMilliseconds(40));

            state = "before-three";
            delayingQueue.AddAfter("three", TimeSpan.FromMilliseconds(1));
            state = "after-three";
            await Task.Delay(TimeSpan.FromMilliseconds(40));

            Assert.Equal(("before-two", "two"), Assert.Single(added));
        }

        [Fact(Skip = "https://github.com/microsoft/reverse-proxy/issues/1357")]
        public async Task NoAddingAfterShutdown()
        {
            var added = new List<string>();
            var fake = new FakeQueue<string>
            {
                OnAdd = added.Add,
            };
            var clock = new FakeSystemClock();
            IDelayingQueue<string> delayingQueue = new DelayingQueue<string>(clock, fake);

            delayingQueue.AddAfter("one", TimeSpan.FromMilliseconds(10));
            delayingQueue.ShutDown();
            delayingQueue.AddAfter("two", TimeSpan.FromMilliseconds(10));
            clock.Advance(TimeSpan.FromMilliseconds(25));
            await Task.Delay(TimeSpan.FromMilliseconds(40));

            Assert.Empty(added);
        }
    }
}
