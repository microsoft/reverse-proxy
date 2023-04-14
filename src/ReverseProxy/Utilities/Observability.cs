using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Yarp.ReverseProxy.Utilities
{
    internal static class Observability
    {
        public static readonly ActivitySource YarpActivitySource = new ActivitySource("Yarp.ReverseProxy");
    }
}
