// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.ReverseProxy.Utilities
{
    public class ThreadStaticRandomTests
    {
        [Fact]
        public void ThreadStaticRandom_Work()
        {
            // Set up the random instance.
            var random = ThreadStaticRandom.Instance;

            // Validate.
            Assert.NotNull(random);
            Assert.IsType<RandomWrapper>(random);

            // Validate random generation.
            var num = random.Next();
            Assert.True(num >= 0);
            num = random.Next(5);
            Assert.InRange(num, 0, 5);
            num = random.Next(0, 5);
            Assert.InRange(num, 0, 5);
        }
    }
}
