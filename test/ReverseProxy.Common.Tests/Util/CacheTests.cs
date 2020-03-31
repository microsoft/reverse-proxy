// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

using FluentAssertions;

using Microsoft.ReverseProxy.Common.Abstractions.Time;
using Microsoft.ReverseProxy.Common.Util;
using Tests.Common;

using Xunit;

namespace Microsoft.ReverseProxy.Common.Tests
{
    public class CacheTests : TestAutoMockBase
    {
        private readonly VirtualMonotonicTimer _timer;
        public CacheTests()
        {
            _timer = new VirtualMonotonicTimer();
            Provide<IMonotonicTimer>(_timer);
        }

        [Fact]
        public void Get_NotExpired_KeyIsPresent()
        {
            // Arrange
            var expirationTimeOffset = TimeSpan.FromMinutes(12);
            var key = "some key";
            var value = "some awesome value";
            var cache = new Cache<string>(_timer, expirationTimeOffset);
            cache.Set(key, value);

            // Act
            var firstPresent = cache.TryGetValue(key, out var firstValueGot);
            _timer.AdvanceClockBy(expirationTimeOffset);
            var secondPresent = cache.TryGetValue(key, out var secondValueGot);

            // Assert
            firstValueGot.Should().Be(value);
            firstPresent.Should().BeTrue();
            secondValueGot.Should().Be(value);
            secondPresent.Should().BeTrue();
        }

        [Fact]
        public void Get_Expired_KeyIsNotPresent()
        {
            // Arrange
            var expirationTimeOffset = TimeSpan.FromMinutes(12);
            var key = "some key";
            var value = "some awesome value";
            var cache = new Cache<string>(_timer, expirationTimeOffset);
            cache.Set(key, value);

            // Act
            var firstPresent = cache.TryGetValue(key, out var firstValueGot);
            _timer.AdvanceClockBy(expirationTimeOffset);
            _timer.AdvanceClockBy(expirationTimeOffset);
            var secondPresent = cache.TryGetValue(key, out var secondValueGot);

            // Assert
            firstValueGot.Should().Be(value);
            firstPresent.Should().BeTrue();
            secondPresent.Should().BeFalse();
        }
    }
}
