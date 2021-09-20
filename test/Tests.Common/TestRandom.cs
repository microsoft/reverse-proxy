// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.Tests.Common
{
    public class TestRandom : Random
    {
        public int[] Sequence { get; set; }
        public int Offset { get; set; }

        public override int Next(int maxValue)
        {
            return Sequence[Offset++];
        }
    }
}
