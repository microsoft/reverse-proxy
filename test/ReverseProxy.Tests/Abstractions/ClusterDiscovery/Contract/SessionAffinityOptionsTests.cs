using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract
{
    public class SessionAffinityOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new SessionAffinityOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            // Arrange
            var sut = new SessionAffinityOptions
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Mode = "mode1"
            };

            // Act
            var clone = sut.DeepClone();

            // Assert
            Assert.NotSame(sut, clone);
            Assert.Equal(sut.Enabled, clone.Enabled);
            Assert.Equal(sut.FailurePolicy, clone.FailurePolicy);
            Assert.Equal(sut.Mode, clone.Mode);
        }

        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            // Arrange
            var options1 = new SessionAffinityOptions
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Mode = "mode1"
            };

            var options2 = new SessionAffinityOptions
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Mode = "mode1"
            };

            // Act
            var equals = SessionAffinityOptions.Equals(options1, options2);

            // Assert
            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            // Arrange
            var options1 = new SessionAffinityOptions
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Mode = "mode1"
            };

            var options2 = new SessionAffinityOptions
            {
                Enabled = false,
                FailurePolicy = "policy2",
                Mode = "mode2"
            };

            // Act
            var equals = SessionAffinityOptions.Equals(options1, options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_First_Null_Returns_False()
        {
            // Arrange
            var options2 = new SessionAffinityOptions
            {
                Enabled = false,
                FailurePolicy = "policy2",
                Mode = "mode2"
            };

            // Act
            var equals = SessionAffinityOptions.Equals(null, options2);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            // Arrange
            var options1 = new SessionAffinityOptions
            {
                Enabled = true,
                FailurePolicy = "policy1",
                Mode = "mode1"
            };

            // Act
            var equals = SessionAffinityOptions.Equals(options1, null);

            // Assert
            Assert.False(equals);
        }

        [Fact]
        public void Equals_Both_Null_Returns_True()
        {
            // Arrange

            // Act
            var equals = SessionAffinityOptions.Equals(null, null);

            // Assert
            Assert.True(equals);
        }
    }
}
