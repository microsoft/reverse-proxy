using System.Collections.Generic;
using k8s.Models;

namespace Yarp.Kubernetes.Controller.Caching;

#if NETCOREAPP3_1
internal class DummyServerCertificateCache : IServerCertificateCache
{
    public void UpdateSecret(string key, V1Secret secret)
    {
    }

    public void RemoveSecret(string key)
    {
    }

    public void UpdateHostMap(IEnumerable<(string hostName, string secretKey)> hostsToAdd, IEnumerable<string> hostsToRemove)
    {
    }
}
#endif
