#if NET5_0_OR_GREATER
using System;
using Xunit;

namespace Yarp.ReverseProxy;

public partial class HttpSysDelegationTests
{
    public class HttpSysFactAttribute : FactAttribute
    {
        public HttpSysFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
            {
                Skip = "Http.sys tests are only supported on Windows";
            }
        }
    }
}
#endif
