namespace Microsoft.ReverseProxy
{
    public static class TestUrlHelper
    {
        public static string GetTestUrl(ServerType serverType)
        {
            return TestUriHelper.BuildTestUri(serverType).ToString();
        }
    }
}
