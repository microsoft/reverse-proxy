// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Abstractions.Tests
{
    public class QuotaOptionsTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new QuotaOptions();
        }

        [Fact]
        public void DeepClone_Works()
        {
            var sut = new QuotaOptions
            {
                Average = 10,
                Burst = 100,
            };

            var clone = sut.DeepClone();

            Assert.NotSame(sut, clone);
            Assert.Equal(sut.Average,clone.Average);
            Assert.Equal(sut.Burst,clone.Burst);
        }

        [Fact]
        public void Equals_Same_Value_Returns_True()
        {
            var options1 = new QuotaOptions
            {
                Average = 10,
                Burst = 100,
            };

            var options2 = new QuotaOptions
            {
                Average = 10,
                Burst = 100,
            };

            var equals = QuotaOptions.Equals(options1, options2);

            Assert.True(equals);
        }

        [Fact]
        public void Equals_Different_Value_Returns_False()
        {
            var options1 = new QuotaOptions
            {
                Average = 10,
                Burst = 100,
            };

            var options2 = new QuotaOptions
            {
                Average = 20,
                Burst = 200,
            };

            var equals = QuotaOptions.Equals(options1, options2);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_First_Null_Returns_False()
        {
            var options2 = new QuotaOptions
            {
                Average = 20,
                Burst = 200,
            };

            var equals = QuotaOptions.Equals(null, options2);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Second_Null_Returns_False()
        {
            var options1 = new QuotaOptions
            {
                Average = 10,
                Burst = 100,
            };

            var equals = QuotaOptions.Equals(options1, null);

            Assert.False(equals);
        }

        [Fact]
        public void Equals_Both_Null_Returns_True()
        {
            var equals = QuotaOptions.Equals(null, null);

            Assert.True(equals);
        }
    }
}
