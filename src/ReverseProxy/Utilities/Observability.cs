using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.Utilities
{
    internal static class Observability
    {
        public static readonly ActivitySource YarpActivitySource = new ActivitySource("Yarp.ReverseProxy");

        public static Activity? GetYarpActivity(this HttpContext context)
        {
            return context.Features.Get<ActivityWrapper>()?.YarpActivity;
        }

        public static void SetYarpActivity(this HttpContext context, Activity? activity)
        {
            if (activity != null)
            {
                context.Features.Set(new ActivityWrapper { YarpActivity = activity });
            }
        }

        private class ActivityWrapper
        {
            internal Activity? YarpActivity;
        }
    }
}
