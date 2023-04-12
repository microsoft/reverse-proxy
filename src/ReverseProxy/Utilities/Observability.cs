using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Yarp.ReverseProxy.Utilities
{
    internal class Observability
    {
        private static ActivitySource _activitySource = new ActivitySource("Yarp.ReverseProxy");

        public static ActivitySource YarpActivitySource => _activitySource;

        public static bool IsListening => _activitySource.HasListeners();
    }
}
