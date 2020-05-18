using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    /// <summary>
    /// Tracks current the request's affinity.
    /// </summary>
    public interface ISessionAffinityFeature
    {
        /// <summary>
        /// Key binding the current request to one or many <see cref="DestinationInfo"/>.
        /// </summary>
        public string DestinationKey { get; set; }

        /// <summary>
        /// Affinity mode.
        /// </summary>
        public SessionAffinityMode Mode { get; set; }

        /// <summary>
        /// Name of a custom header storing an affinity key to be used when <see cref="Mode"/> is set to <see cref="SessionAffinityMode.CustomHeader"/>.
        /// </summary>
        public string CustomHeaderName { get; set; }
    }
}
