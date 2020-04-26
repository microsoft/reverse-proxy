using System;
using System.Threading;

namespace ReverseProxy.Core.Service.Proxy.LoadBalancingStrategies
{
    public static class ThreadLocalRandom
    {
        private static int _seed = Environment.TickCount;
        private static readonly ThreadLocal<Random> _random = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref _seed)));

        public static Random Current => _random.Value;
    }
}
