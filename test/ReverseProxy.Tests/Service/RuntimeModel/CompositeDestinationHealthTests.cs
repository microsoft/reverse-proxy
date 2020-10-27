// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;
using Xunit;

namespace Microsoft.ReverseProxy.Service.RuntimeModel
{
    public class CompositeDestinationHealthTests
    {
        [Theory]
        [InlineData(DestinationHealth.Unknown, DestinationHealth.Unknown, DestinationHealth.Unknown)]
        [InlineData(DestinationHealth.Unknown, DestinationHealth.Healthy, DestinationHealth.Healthy)]
        [InlineData(DestinationHealth.Unknown, DestinationHealth.Unhealthy, DestinationHealth.Unhealthy)]
        [InlineData(DestinationHealth.Healthy, DestinationHealth.Unknown, DestinationHealth.Healthy)]
        [InlineData(DestinationHealth.Healthy, DestinationHealth.Healthy, DestinationHealth.Healthy)]
        [InlineData(DestinationHealth.Healthy, DestinationHealth.Unhealthy, DestinationHealth.Unhealthy)]
        [InlineData(DestinationHealth.Unhealthy, DestinationHealth.Unknown, DestinationHealth.Unhealthy)]
        [InlineData(DestinationHealth.Unhealthy, DestinationHealth.Healthy, DestinationHealth.Unhealthy)]
        [InlineData(DestinationHealth.Unhealthy, DestinationHealth.Unhealthy, DestinationHealth.Unhealthy)]
        public void Current_CalculatedAsBooleanOrOfActiveAndPassiveStates(DestinationHealth passive, DestinationHealth active, DestinationHealth expectedCurrent)
        {
            var compositeHealth = new CompositeDestinationHealth(passive, active);
            Assert.Equal(expectedCurrent, compositeHealth.Current);
        }

        [Theory]
        [InlineData(DestinationHealth.Unknown, DestinationHealth.Healthy)]
        [InlineData(DestinationHealth.Unknown, DestinationHealth.Unhealthy)]
        [InlineData(DestinationHealth.Healthy, DestinationHealth.Unknown)]
        [InlineData(DestinationHealth.Healthy, DestinationHealth.Unhealthy)]
        [InlineData(DestinationHealth.Unhealthy, DestinationHealth.Unknown)]
        [InlineData(DestinationHealth.Unhealthy, DestinationHealth.Healthy)]
        public void ChangePassive_NewPassiveValueGiven_PassiveChangedActiveStaysSame(DestinationHealth oldPassive, DestinationHealth newPassive)
        {
            var compositeHealth = new CompositeDestinationHealth(oldPassive, DestinationHealth.Healthy);
            compositeHealth = compositeHealth.ChangePassive(newPassive);

            Assert.Equal(newPassive, compositeHealth.Passive);
            Assert.Equal(DestinationHealth.Healthy, compositeHealth.Active);
        }

        [Theory]
        [InlineData(DestinationHealth.Unknown, DestinationHealth.Healthy)]
        [InlineData(DestinationHealth.Unknown, DestinationHealth.Unhealthy)]
        [InlineData(DestinationHealth.Healthy, DestinationHealth.Unknown)]
        [InlineData(DestinationHealth.Healthy, DestinationHealth.Unhealthy)]
        [InlineData(DestinationHealth.Unhealthy, DestinationHealth.Unknown)]
        [InlineData(DestinationHealth.Unhealthy, DestinationHealth.Healthy)]
        public void ChangeActive_NewActiveValueGiven_ActiveChangedPassiveStaysSame(DestinationHealth oldActive, DestinationHealth newActive)
        {
            var compositeHealth = new CompositeDestinationHealth(DestinationHealth.Healthy, oldActive);
            compositeHealth = compositeHealth.ChangeActive(newActive);

            Assert.Equal(newActive, compositeHealth.Active);
            Assert.Equal(DestinationHealth.Healthy, compositeHealth.Passive);
        }
    }
}
