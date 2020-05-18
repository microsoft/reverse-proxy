namespace Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract
{
    /// <summary>
    /// Location of a session key for affinitized requests
    /// </summary>
    public enum SessionAffinityMode
    {
        /// <summary>
        /// Session key is stored as a cookie.
        /// </summary>
        Cookie,

        /// <summary>
        /// Session key is stored on a custom header.
        /// </summary>
        CustomHeader
    }
}
