using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Yarp.Kubernetes.Controller.Caching;

public class IngressTlsData
{
    internal IngressTlsData([NotNull] string namespaceName, [NotNull] string secretName, IEnumerable<string> hosts)
    {
#pragma warning disable CA1308 // Normalize strings to uppercase
        if (secretName.Contains('/', StringComparison.OrdinalIgnoreCase))
        {
            SecretKey = secretName.ToLowerInvariant();
        }
        else
        {
            SecretKey = $"{namespaceName}/{secretName}".ToLowerInvariant();
        }
#pragma warning restore CA1308 // Normalize strings to uppercase

        Hosts = hosts?.ToArray() ?? Array.Empty<string>();
    }

    public string SecretKey { get; }

    public IReadOnlyCollection<string> Hosts { get; }
}
