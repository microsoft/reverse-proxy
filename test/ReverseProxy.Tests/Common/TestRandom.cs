using System;

namespace Microsoft.ReverseProxy.Common
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
