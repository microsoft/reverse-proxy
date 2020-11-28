using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.ReverseProxy.Utilities
{
    public class ConfigurationReadingExtensionsTests
    {
        [Fact]
        public void ReadInt32_NegativeNumber()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Key"] = "-1"
                })
                .Build();

            var number = configuration.ReadInt32("Key");

            Assert.Equal(-1, number);
        }
    }
}
