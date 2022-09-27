// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Connections;

namespace Yarp.Kubernetes.Controller.Certificates;

internal class ServerCertificateSelector : IServerCertificateSelector
{
    private const string FALLBACK_DOMAIN = "_default";
    private Dictionary<NamespacedName, X509Certificate2> _defaultCertificate = new Dictionary<NamespacedName, X509Certificate2>();
    private Dictionary<string, NamespacedName> _mapping = new Dictionary<string, NamespacedName>();

    public void AddCertificate(NamespacedName certificateName, X509Certificate2 certificate)
    {
        AddCertificate(certificateName, certificate, null);
    }

    public void AddCertificate(NamespacedName certificateName, X509Certificate2 certificate, string domainName)
    {
        if (domainName == null) {
            domainName = FALLBACK_DOMAIN;
        }
        _defaultCertificate[certificateName] = certificate;
        _mapping[domainName] = certificateName;
    }

    public X509Certificate2 GetCertificate(ConnectionContext connectionContext, string domainName)
    {
        if (!_mapping.TryGetValue(domainName ?? FALLBACK_DOMAIN, out var mapp)) {
            return _defaultCertificate[_mapping[FALLBACK_DOMAIN]];
        }

        return _defaultCertificate[mapp];
    }

    public void RemoveCertificate(NamespacedName certificateName)
    {
        var keys = _mapping.Keys.Where(x => _mapping[x] == certificateName);
        foreach(var key in keys) {
            _defaultCertificate.Remove(_mapping[key]);
            _mapping.Remove(key);
        }
    }
}
