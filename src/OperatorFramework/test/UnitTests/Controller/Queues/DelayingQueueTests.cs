// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Controller.Queues
{
    [TestClass]
    public class DelayingQueueTests
    {
        [TestMethod]
        public void DelayingQueuePassesCallsThrough()
        {
            // arrange
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

            // act
            delayingQueue.Add("one");
            delayingQueue.Done("two");
            var len = delayingQueue.Len();

            // assert
            added.ShouldHaveSingleItem().ShouldBe("one");
            doned.ShouldHaveSingleItem().ShouldBe("two");
            len.ShouldBe(42);
        }

        [TestMethod]
        public async Task DelayingQueueAddsWhenTimePasses()
        {
            // arrange            
            var added = new List<string>();
            var fake = new FakeQueue<string>
            {
                OnAdd = added.Add,
            };
            var clock = new FakeSystemClock();
            IDelayingQueue<string> delayingQueue = new DelayingQueue<string>(clock, fake);

            // act
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

            // assert
            countAfter25ms.ShouldBe(0);
            countAfter55ms.ShouldBe(1);
            countAfter80ms.ShouldBe(2);
            countAfter105ms.ShouldBe(3);
            countAfter135ms.ShouldBe(4);
            added.ShouldBe(new[] { "50ms", "75ms", "100ms", "125ms" }, ignoreOrder: false);
        }

        [TestMethod]
        public async Task ZeroDelayAddsInline()
        {
            // arrange            
            var state = "setup";
            var added = new List<(string state, string item)>();
            var fake = new FakeQueue<string>
            {
                OnAdd = item => added.Add((state, item)),
            };
            var clock = new FakeSystemClock();
            IDelayingQueue<string> delayingQueue = new DelayingQueue<string>(clock, fake);

            // act
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

            // assert
            added.ShouldHaveSingleItem().ShouldBe(("before-two", "two"));
        }

        [TestMethod]
        public async Task NoAddingAfterShutdown()
        {
            // arrange            
            var added = new List<string>();
            var fake = new FakeQueue<string>
            {
                OnAdd = added.Add,
            };
            var clock = new FakeSystemClock();
            IDelayingQueue<string> delayingQueue = new DelayingQueue<string>(clock, fake);

            // act
            delayingQueue.AddAfter("one", TimeSpan.FromMilliseconds(10));
            delayingQueue.ShutDown();
            delayingQueue.AddAfter("two", TimeSpan.FromMilliseconds(10));
            clock.Advance(TimeSpan.FromMilliseconds(25));
            await Task.Delay(TimeSpan.FromMilliseconds(40));

            // assert
            added.ShouldBeEmpty();
        }
    }
}
