using System;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Common
{
    public class TestRandomFactory : IRandomFactory
    {
        public TestRandom Instance { get; set; }

        public Random CreateRandomInstance()
        {
            return Instance;
        }
    }
}
