using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Connections;

namespace Yarp.Kubernetes.Controller.Caching;

public interface IServerCertificateResolver
{
    X509Certificate2 GetCertificate(ConnectionContext connectionContext, string name);
}
