namespace Microsoft.ReverseProxy
{
    public static class TestUrlHelper
    {
        public static string GetTestUrl()
        {
            return TestUriHelper.BuildTestUri().ToString();
        }
    }
}
