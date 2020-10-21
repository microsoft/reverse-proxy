// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using Microsoft.ReverseProxy.Utilities;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Management
{
    public class EntityActionSchedulerTests
    {
        [Fact]
        public void Schedule_AutoStartEnabledRunOnceDisabled_StartsAutomaticallyAndRunsIndefinitely()
        {
            var invoked = new AutoResetEvent(false);
            var entity0 = new Entity { Id = "entity0" };
            var period0 = TimeSpan.FromMilliseconds(1100);
            var entity1 = new Entity { Id = "entity1" };
            var period1 = TimeSpan.FromMilliseconds(700);
            var timeout = TimeSpan.FromSeconds(2);
            Entity lastInvokedEntity = null;
            using var scheduler = new EntityActionScheduler<Entity>(e =>
            {
                lastInvokedEntity = e;
                invoked.Set();
            }, autoStart: true, runOnce: false, new UptimeClock());

            scheduler.ScheduleEntity(entity0, period0);
            scheduler.ScheduleEntity(entity1, period1);

            VerifyEntities(scheduler, entity0, entity1);

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity1, lastInvokedEntity);

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity0, lastInvokedEntity);

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity1, lastInvokedEntity);

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity1, lastInvokedEntity);

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity0, lastInvokedEntity);

            VerifyEntities(scheduler, entity0, entity1);
        }

        [Fact]
        public void Schedule_AutoStartDisabledRunOnceEnabled_StartsManuallyAndRunsEachRegistrationOnlyOnce()
        {
            var invoked = new AutoResetEvent(false);
            var entity0 = new Entity { Id = "entity0" };
            var period0 = TimeSpan.FromMilliseconds(1100);
            var entity1 = new Entity { Id = "entity1" };
            var period1 = TimeSpan.FromMilliseconds(700);
            var timeout = TimeSpan.FromSeconds(2);
            Entity lastInvokedEntity = null;
            using var scheduler = new EntityActionScheduler<Entity>(e =>
            {
                lastInvokedEntity = e;
                invoked.Set();
            }, autoStart: false, runOnce: true, new UptimeClock());

            scheduler.ScheduleEntity(entity0, period0);
            scheduler.ScheduleEntity(entity1, period1);

            Assert.False(invoked.WaitOne(timeout));

            scheduler.Start();

            VerifyEntities(scheduler, entity0, entity1);

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity1, lastInvokedEntity);

            VerifyEntities(scheduler, entity0);

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity0, lastInvokedEntity);

            Assert.Empty(scheduler.GetScheduledEntities());
        }

        [Fact]
        public void Unschedule_EntityUnscheduledBeforeFirstCall_CallbackNotInvoked()
        {
            var invoked = new AutoResetEvent(false);
            var entity0 = new Entity { Id = "entity0" };
            var period0 = TimeSpan.FromMilliseconds(1100);
            var entity1 = new Entity { Id = "entity1" };
            var period1 = TimeSpan.FromMilliseconds(700);
            var timeout = TimeSpan.FromSeconds(2);
            Entity lastInvokedEntity = null;
            using var scheduler = new EntityActionScheduler<Entity>(e =>
            {
                lastInvokedEntity = e;
                invoked.Set();
            }, autoStart: false, runOnce: false, new UptimeClock());

            scheduler.ScheduleEntity(entity0, period0);
            scheduler.ScheduleEntity(entity1, period1);

            VerifyEntities(scheduler, entity0, entity1);

            scheduler.UnscheduleEntity(entity1);
            VerifyEntities(scheduler, entity0);

            scheduler.Start();

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity0, lastInvokedEntity);

            VerifyEntities(scheduler, entity0);
        }

        [Fact]
        public void Unschedule_EntityUnscheduledAfterFirstCall_CallbackInvokedOnlyOnce()
        {
            var invoked = new AutoResetEvent(false);
            var entity0 = new Entity { Id = "entity0" };
            var period0 = TimeSpan.FromMilliseconds(1100);
            var entity1 = new Entity { Id = "entity1" };
            var period1 = TimeSpan.FromMilliseconds(700);
            var timeout = TimeSpan.FromSeconds(2);
            Entity lastInvokedEntity = null;
            using var scheduler = new EntityActionScheduler<Entity>(e =>
            {
                lastInvokedEntity = e;
                invoked.Set();
            }, autoStart: true, runOnce: false, new UptimeClock());

            scheduler.ScheduleEntity(entity0, period0);
            scheduler.ScheduleEntity(entity1, period1);

            VerifyEntities(scheduler, entity0, entity1);

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity1, lastInvokedEntity);

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity0, lastInvokedEntity);

            scheduler.UnscheduleEntity(entity1);
            VerifyEntities(scheduler, entity0);

            Assert.True(invoked.WaitOne(timeout));
            Assert.Same(entity0, lastInvokedEntity);

            VerifyEntities(scheduler, entity0);
        }

        private void VerifyEntities(EntityActionScheduler<Entity> scheduler, params Entity[] entities)
        {
            var scheduledEntities = scheduler.GetScheduledEntities().ToList();
            Assert.Equal(entities.Length, scheduledEntities.Count);
            foreach(var entity in entities)
            {
                Assert.Contains(entity, scheduledEntities);
            }
        }

        private class Entity
        {
            public string Id { get; set; }
        }
    }
}
