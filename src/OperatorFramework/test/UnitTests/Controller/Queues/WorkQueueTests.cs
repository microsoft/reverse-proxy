// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Controller.Queues
{
    [TestClass]
    public class WorkQueueTests
    {
        public CancellationTokenSource Cancellation { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            Cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Cancellation.Dispose();
        }

        [TestMethod]
        public async Task NormalUsageIsAddGetDone()
        {
            // arrange
            using IWorkQueue<string> queue = new WorkQueue<string>();

            // act
            queue.Len().ShouldBe(0);
            queue.Add("one");
            queue.Len().ShouldBe(1);
            queue.Add("two");
            queue.Len().ShouldBe(2);
            var (item1, shutdown1) = await queue.GetAsync(Cancellation.Token);
            queue.Len().ShouldBe(1);
            queue.Done(item1);
            queue.Len().ShouldBe(1);
            var (item2, shutdown2) = await queue.GetAsync(Cancellation.Token);
            queue.Len().ShouldBe(0);
            queue.Done(item2);
            queue.Len().ShouldBe(0);

            // assert
            item1.ShouldBe("one");
            shutdown1.ShouldBeFalse();
            item2.ShouldBe("two");
            shutdown2.ShouldBeFalse();
        }

        [TestMethod]
        public void AddingSameItemAgainHasNoEffect()
        {
            // arrange
            using IWorkQueue<string> queue = new WorkQueue<string>();

            // act
            var len1 = queue.Len();
            queue.Add("one");
            var len2 = queue.Len();
            queue.Add("one");
            var len3 = queue.Len();

            // assert
            len1.ShouldBe(0);
            len2.ShouldBe(1);
            len3.ShouldBe(1);
        }

        [TestMethod]
        public async Task CallingAddWhileItemIsBeingProcessedHasNoEffect()
        {
            // arrange
            using IWorkQueue<string> queue = new WorkQueue<string>();

            // act
            var lenOriginal = queue.Len();
            queue.Add("one");
            var lenAfterAdd = queue.Len();
            var (item1, _) = await queue.GetAsync(Cancellation.Token);
            var lenAfterGet = queue.Len();
            queue.Add("one");
            var lenAfterAddAgain = queue.Len();

            // assert
            item1.ShouldBe("one");
            lenOriginal.ShouldBe(0);
            lenAfterAdd.ShouldBe(1);
            lenAfterGet.ShouldBe(0);
            lenAfterAddAgain.ShouldBe(0);

            queue.Len().ShouldBe(0);
        }

        [TestMethod]
        public async Task ItemCanBeAddedAgainAfterDoneIsCalled()
        {
            // arrange
            using IWorkQueue<string> queue = new WorkQueue<string>();

            // act
            var lenOriginal = queue.Len();
            queue.Add("one");
            var lenAfterAdd = queue.Len();
            var (item1, _) = await queue.GetAsync(Cancellation.Token);
            var lenAfterGet = queue.Len();
            queue.Done(item1);
            var lenAfterDone = queue.Len();
            queue.Add("one");
            var lenAfterAddAgain = queue.Len();

            // assert
            item1.ShouldBe("one");
            lenOriginal.ShouldBe(0);
            lenAfterAdd.ShouldBe(1);
            lenAfterGet.ShouldBe(0);
            lenAfterDone.ShouldBe(0);
            lenAfterAddAgain.ShouldBe(1);

            queue.Len().ShouldBe(1);
        }

        [TestMethod]
        public async Task IfAddWasCalledDuringProcessingThenItemIsRequeuedByDone()
        {
            // arrange
            using IWorkQueue<string> queue = new WorkQueue<string>();

            // act
            var lenOriginal = queue.Len();
            queue.Add("one");
            var lenAfterAdd = queue.Len();
            var (item1, _) = await queue.GetAsync(Cancellation.Token);
            var lenAfterGet = queue.Len();
            queue.Add("one");
            var lenAfterAddAgain = queue.Len();
            queue.Done(item1);
            var lenAfterDone = queue.Len();
            var (item2, _) = await queue.GetAsync(Cancellation.Token);
            var lenAfterGetAgain = queue.Len();

            // assert
            item1.ShouldBe("one");
            item2.ShouldBe("one");
            lenOriginal.ShouldBe(0);
            lenAfterAdd.ShouldBe(1);
            lenAfterGet.ShouldBe(0);
            lenAfterAddAgain.ShouldBe(0);
            lenAfterDone.ShouldBe(1);
            lenAfterGetAgain.ShouldBe(0);

            queue.Len().ShouldBe(0);
        }


        [TestMethod]
        public async Task GetCompletesOnceAddIsCalled()
        {
            // arrange
            using IWorkQueue<string> queue = new WorkQueue<string>();

            // act
            var getTask = queue.GetAsync(Cancellation.Token);
            queue.Len().ShouldBe(0);
            getTask.IsCompleted.ShouldBeFalse();

            queue.Add("one");
            var (item1, _) = await getTask;
            queue.Len().ShouldBe(0);
            getTask.IsCompleted.ShouldBeTrue();

            // assert
            item1.ShouldBe("one");
            queue.Len().ShouldBe(0);
        }

        [TestMethod]
        public async Task GetReturnsShutdownTrueAfterShutdownIsCalled()
        {
            // arrange
            using IWorkQueue<string> queue = new WorkQueue<string>();

            // act
            var getTask = queue.GetAsync(Cancellation.Token);
            queue.Len().ShouldBe(0);
            getTask.IsCompleted.ShouldBeFalse();

            queue.ShutDown();
            var (item1, shutdown1) = await getTask;
            queue.Len().ShouldBe(0);
            getTask.IsCompleted.ShouldBeTrue();

            // assert
            shutdown1.ShouldBeTrue();
            queue.Len().ShouldBe(0);
        }

        [TestMethod]
        public void ShuttingDownReturnsTrueAfterShutdownIsCalled()
        {
            // arrange
            using IWorkQueue<string> queue = new WorkQueue<string>();

            // act
            var shuttingDownBefore = queue.ShuttingDown();
            queue.ShutDown();
            var shuttingDownAfter = queue.ShuttingDown();

            // assert
            shuttingDownBefore.ShouldBeFalse();
            shuttingDownAfter.ShouldBeTrue();
        }
    }
}
