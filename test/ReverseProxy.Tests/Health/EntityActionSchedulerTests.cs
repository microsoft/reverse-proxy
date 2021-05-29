// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Health.Tests
{
    // It uses a real TimerFactory to verify scheduling work E2E.
    public class EntityActionSchedulerTests
    {
        private const long Period0 = 20000;
        private const long Period1 = 10000;

        [Fact]
        public void Schedule_AutoStartEnabledRunOnceDisabled_StartsAutomaticallyAndRunsIndefinitely()
        {
            var entity0 = new Entity { Id = "entity0" };
            var entity1 = new Entity { Id = "entity1" };
            var timerFactory = new TestTimerFactory();
            Entity lastInvokedEntity = null;
            using var scheduler = new EntityActionScheduler<Entity>(e =>
            {
                lastInvokedEntity = e;
                return Task.CompletedTask;
            }, autoStart: true, runOnce: false, timerFactory);

            scheduler.ScheduleEntity(entity0, TimeSpan.FromMilliseconds(20000));
            scheduler.ScheduleEntity(entity1, TimeSpan.FromMilliseconds(10000));

            VerifyEntities(scheduler, entity0, entity1);
            Assert.Equal(2, timerFactory.Count);
            timerFactory.VerifyTimer(0, Period0);
            timerFactory.VerifyTimer(1, Period1);

            timerFactory.FireTimer(1);
            Assert.Same(entity1, lastInvokedEntity);

            timerFactory.FireTimer(0);
            Assert.Same(entity0, lastInvokedEntity);

            timerFactory.FireTimer(1);
            Assert.Same(entity1, lastInvokedEntity);

            timerFactory.FireTimer(0);
            Assert.Same(entity0, lastInvokedEntity);

            VerifyEntities(scheduler, entity0, entity1);
            Assert.Equal(2, timerFactory.Count);
            timerFactory.VerifyTimer(0, Period0);
            timerFactory.VerifyTimer(1, Period1);
        }

        [Fact]
        public void Schedule_AutoStartDisabledRunOnceEnabled_StartsManuallyAndRunsEachRegistrationOnlyOnce()
        {
            var entity0 = new Entity { Id = "entity0" };
            var entity1 = new Entity { Id = "entity1" };
            Entity lastInvokedEntity = null;
            var timerFactory = new TestTimerFactory();
            using var scheduler = new EntityActionScheduler<Entity>(e =>
            {
                lastInvokedEntity = e;
                return Task.CompletedTask;
            }, autoStart: false, runOnce: true, timerFactory);

            scheduler.ScheduleEntity(entity0, TimeSpan.FromMilliseconds(Period0));
            scheduler.ScheduleEntity(entity1, TimeSpan.FromMilliseconds(Period1));
            Assert.Equal(2, timerFactory.Count);
            timerFactory.VerifyTimer(0, Timeout.Infinite);
            timerFactory.VerifyTimer(1, Timeout.Infinite);

            scheduler.Start();

            VerifyEntities(scheduler, entity0, entity1);
            Assert.Equal(2, timerFactory.Count);
            timerFactory.VerifyTimer(0, Period0);
            timerFactory.VerifyTimer(1, Period1);

            timerFactory.FireTimer(1);

            Assert.Same(entity1, lastInvokedEntity);

            VerifyEntities(scheduler, entity0);

            timerFactory.FireTimer(0);

            Assert.Same(entity0, lastInvokedEntity);

            Assert.False(scheduler.IsScheduled(entity0));
            Assert.False(scheduler.IsScheduled(entity1));
            timerFactory.AssertTimerDisposed(0);
            timerFactory.AssertTimerDisposed(1);
        }

        [Fact]
        public void Unschedule_EntityUnscheduledBeforeFirstCall_CallbackNotInvoked()
        {
            var entity0 = new Entity { Id = "entity0" };
            var entity1 = new Entity { Id = "entity1" };
            Entity lastInvokedEntity = null;
            var timerFactory = new TestTimerFactory();
            using var scheduler = new EntityActionScheduler<Entity>(e =>
            {
                lastInvokedEntity = e;
                return Task.CompletedTask;
            }, autoStart: false, runOnce: false, timerFactory);

            scheduler.ScheduleEntity(entity0, TimeSpan.FromMilliseconds(Period0));
            scheduler.ScheduleEntity(entity1, TimeSpan.FromMilliseconds(Period1));

            VerifyEntities(scheduler, entity0, entity1);
            Assert.Equal(2, timerFactory.Count);
            timerFactory.VerifyTimer(0, Timeout.Infinite);
            timerFactory.VerifyTimer(1, Timeout.Infinite);

            scheduler.UnscheduleEntity(entity1);
            VerifyEntities(scheduler, entity0);
            timerFactory.AssertTimerDisposed(1);

            scheduler.Start();

            timerFactory.VerifyTimer(0, Period0);

            timerFactory.FireTimer(0);

            Assert.Same(entity0, lastInvokedEntity);

            VerifyEntities(scheduler, entity0);
        }

        [Fact]
        public void Unschedule_EntityUnscheduledAfterFirstCall_CallbackInvokedOnlyOnce()
        {
            var entity0 = new Entity { Id = "entity0" };
            var entity1 = new Entity { Id = "entity1" };
            Entity lastInvokedEntity = null;
            var timerFactory = new TestTimerFactory();
            using var scheduler = new EntityActionScheduler<Entity>(e =>
            {
                lastInvokedEntity = e;
                return Task.CompletedTask;
            }, autoStart: true, runOnce: false, timerFactory);

            scheduler.ScheduleEntity(entity0, TimeSpan.FromMilliseconds(Period0));
            scheduler.ScheduleEntity(entity1, TimeSpan.FromMilliseconds(Period1));

            VerifyEntities(scheduler, entity0, entity1);

            timerFactory.FireTimer(1);

            Assert.Same(entity1, lastInvokedEntity);

            timerFactory.FireTimer(0);

            Assert.Same(entity0, lastInvokedEntity);

            scheduler.UnscheduleEntity(entity1);
            VerifyEntities(scheduler, entity0);
            timerFactory.AssertTimerDisposed(1);

            timerFactory.FireTimer(0);

            Assert.Same(entity0, lastInvokedEntity);

            VerifyEntities(scheduler, entity0);
        }

        [Fact]
        public void ChangePeriod_PeriodChangedTimerNotStarted_PeriodChangedBeforeFirstCall()
        {
            var entity = new Entity { Id = "entity0" };
            Entity lastInvokedEntity = null;
            var timerFactory = new TestTimerFactory();
            using var scheduler = new EntityActionScheduler<Entity>(e =>
            {
                lastInvokedEntity = e;
                return Task.CompletedTask;
            }, autoStart: false, runOnce: false, timerFactory);

            scheduler.ScheduleEntity(entity, TimeSpan.FromMilliseconds(Period0));
            timerFactory.VerifyTimer(0, Timeout.Infinite);

            var newPeriod = TimeSpan.FromMilliseconds(Period1);
            scheduler.ChangePeriod(entity, newPeriod);
            timerFactory.VerifyTimer(0, Timeout.Infinite);

            scheduler.Start();

            timerFactory.VerifyTimer(0, Period1);

            timerFactory.FireTimer(0);

            Assert.Same(entity, lastInvokedEntity);
        }

        [Fact]
        public void ChangePeriod_TimerStartedPeriodChangedAfterFirstCall_PeriodChangedBeforeNextCall()
        {
            var entity = new Entity { Id = "entity0" };
            Entity lastInvokedEntity = null;
            var timerFactory = new TestTimerFactory();
            using var scheduler = new EntityActionScheduler<Entity>(e =>
            {
                lastInvokedEntity = e;
                return Task.CompletedTask;
            }, autoStart: true, runOnce: false, timerFactory);

            scheduler.ScheduleEntity(entity, TimeSpan.FromMilliseconds(Period0));
            timerFactory.VerifyTimer(0, Period0);

            timerFactory.FireTimer(0);

            var newPeriod = TimeSpan.FromMilliseconds(Period1);
            scheduler.ChangePeriod(entity, newPeriod);

            timerFactory.VerifyTimer(0, Period1);
            Assert.Same(entity, lastInvokedEntity);
        }

        private void VerifyEntities(EntityActionScheduler<Entity> scheduler, params Entity[] entities)
        {
            var actualCount = 0;
            foreach (var entity in entities)
            {
                Assert.True(scheduler.IsScheduled(entity));
                actualCount++;
            }
            Assert.Equal(entities.Length, actualCount);
        }

        private class Entity
        {
            public string Id { get; set; }
        }
    }
}
