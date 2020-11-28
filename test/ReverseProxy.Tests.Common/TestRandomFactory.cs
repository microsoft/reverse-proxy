// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Common.Tests
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
