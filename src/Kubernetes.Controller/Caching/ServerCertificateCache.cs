using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using k8s.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Yarp.Kubernetes.Controller.Caching;

#if NET5_0_OR_GREATER
public sealed class ServerCertificateCache : IServerCertificateResolver, IServerCertificateCache, IDisposable
{
    private const string TlsCertKey = "tls.crt";
    private const string TlsPrivateKeyKey = "tls.key";

    private readonly YarpOptions _options;
    private readonly ILogger<ServerCertificateCache> _logger;

    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    private readonly Dictionary<string, V1Secret> _secretCache = new Dictionary<string, V1Secret>();
    private readonly Dictionary<string, X509Certificate2> _certificatesByKey = new Dictionary<string, X509Certificate2>();
    private readonly Dictionary<string, string> _hostToKey = new Dictionary<string, string>();

    private X509Certificate2 _defaultCertificate;

    public ServerCertificateCache(IOptions<YarpOptions> options, ILogger<ServerCertificateCache> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public X509Certificate2 GetCertificate(ConnectionContext connectionContext, string name)
    {
        _lock.EnterReadLock();

        try
        {
            // TODO Wildcard support!
            if (!_hostToKey.TryGetValue(name, out var secretKey))
            {
                // No entry found for this host
                return _defaultCertificate;
            }

            if (_certificatesByKey.TryGetValue(secretKey, out var certificate))
            {
                // Matched host to secret key to certificate
                return certificate;
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // Fallback to our default
        return _defaultCertificate;
    }

    public void UpdateSecret(string key, V1Secret secret)
    {
        _logger.LogTrace("Updating certificate with key {Key}", key);

        _lock.EnterWriteLock();

        try
        {
            if (string.Equals(key, _options.DefaultSslCertificate, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Storing default certificate as key {Key}", key);

                var certificate = ConvertCertificate(key, secret);
                if (certificate != null)
                {
                    _defaultCertificate = certificate;
                    _certificatesByKey[key] = certificate;
                }
            }
            else
            {
                // Save this secret for later. It will be better if we can retrieve via Kubernetes client only when needed.
                _secretCache[key] = secret;

                if (_hostToKey.Values.Contains(key))
                {
                    // An ingress is referencing this secret, so we need to update it.
                    var certificate = ConvertCertificate(key, secret);
                    if (certificate != null)
                    {
                        _certificatesByKey[key] = certificate;
                    }
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RemoveSecret(string key)
    {
        _logger.LogTrace("Removing certificate with key {Key}", key);

        _lock.EnterWriteLock();

        try
        {
            _certificatesByKey.Remove(key);
            _secretCache.Remove(key);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void UpdateHostMap(IEnumerable<(string hostName, string secretKey)> hostsToAdd, IEnumerable<string> hostsToRemove)
    {
        _lock.EnterWriteLock();

        try
        {
            if (hostsToAdd != null)
            {
                foreach (var (hostName, secretKey) in hostsToAdd)
                {
                    _hostToKey[hostName] = secretKey;

                    if (!_certificatesByKey.ContainsKey(secretKey) && _secretCache.TryGetValue(secretKey, out var secret))
                    {
                        // An ingress is referencing this secret, so we need to update it
                        var certificate = ConvertCertificate(secretKey, secret);
                        if (certificate != null)
                        {
                            _certificatesByKey[secretKey] = certificate;
                        }
                    }
                }
            }

            if (hostsToRemove != null)
            {
                foreach (var h in hostsToRemove)
                {
                    _hostToKey.Remove(h);
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        // TODO dispose of all certificates
        _lock?.Dispose();
    }

    private X509Certificate2 ConvertCertificate(string key, V1Secret secret)
    {
        try
        {
            var cert = secret?.Data[TlsCertKey];
            var privateKey = secret?.Data[TlsPrivateKeyKey];

            if (cert == null || cert.Length == 0 || privateKey == null || privateKey.Length == 0)
            {
                _logger.LogWarning("TLS secret with key '{Key}' contains invalid data.", key);
                return null;
            }

            using var convertedCertificate = X509Certificate2.CreateFromPem(cert.Select(c => (char)c).ToArray(), privateKey.Select(c => (char)c).ToArray());

            // Cert needs converting. Read https://github.com/dotnet/runtime/issues/23749#issuecomment-388231655
            return new X509Certificate2(convertedCertificate.Export(X509ContentType.Pkcs12));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert secret with key '{Key}'", key);
        }

        return null;
    }
}
#endif
