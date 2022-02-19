using System.Collections.Generic;
using k8s.Models;

namespace Yarp.Kubernetes.Controller.Caching;

public interface IServerCertificateCache
{
    void UpdateSecret(string key, V1Secret secret);

    void RemoveSecret(string key);

    void UpdateHostMap(IEnumerable<(string hostName, string secretKey)> hostsToAdd, IEnumerable<string> hostsToRemove);
}
