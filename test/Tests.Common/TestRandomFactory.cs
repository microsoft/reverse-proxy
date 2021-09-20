// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.Tests.Common
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
