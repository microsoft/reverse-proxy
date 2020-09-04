# Proxy HTTP Client Configuration

Introduced: preview5

## Introduction
Each [Cluster](xref:Microsoft.ReverseProxy.Abstractions.Cluster) has a dedicated [HttpMessageInvoker](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessageinvoker?view=netcore-3.1) instance used to proxy requests to its [Destination](xref:Microsoft.ReverseProxy.Abstractions.Destination)s. The configuration is defined per cluster. On YARP startup, all [Cluster](xref:Microsoft.ReverseProxy.Abstractions.Cluster)s get new [HttpMessageInvoker](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessageinvoker?view=netcore-3.1) instances, however if later the [Cluster](xref:Microsoft.ReverseProxy.Abstractions.Cluster) configuration gets changed the [IProxyHttpClientFactory](xref:Microsoft.ReverseProxy.Service.Proxy.Infrastructure.IProxyHttpClientFactory) will re-run and decide if it should create a new [HttpMessageInvoker](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessageinvoker?view=netcore-3.1) or keep using the existing one. The default [IProxyHttpClientFactory](xref:Microsoft.ReverseProxy.Service.Proxy.Infrastructure.IProxyHttpClientFactory) implementation creates a new [HttpMessageInvoker](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessageinvoker?view=netcore-3.1) when there are changes to the [ProxyHttpClientOptions](xref:Microsoft.ReverseProxy.Abstractions.ProxyHttpClientOptions).

The configuration is represented differently if you're using the [IConfiguration](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.configuration.iconfiguration?view=dotnet-plat-ext-3.1) model or the code-first model.

## IConfiguration
HTTP client configuration contract consists of [ProxyHttpClientData](xref:Microsoft.ReverseProxy.Configuration.Contract.ProxyHttpClientData) and [CertificateConfigData](xref:Microsoft.ReverseProxy.Configuration.Contract.CertificateConfigData) types defining the following configuration schema. These types are focused on defining serializable configuration. The code based HTTP client configuration model is described below in the "Code Configuration" section.
```JSON
"HttpClientOptions": {
    "SslProtocols": [ "<protocol-names>" ],
    "MaxConnectionsPerServer": "<int>",
    "ValidateRemoteCertificate": "<bool>",
    "ClientCertificate": {
        "Path": "<string>",
        "KeyPath": "<string>",
        "Password": "<string>",
        "Subject": "<string>",
        "Store": "<string>",
        "Location": "<string>",
        "AllowInvalid": "<bool>"
    }
}
```
Configuration settings:
- SslProtocols - [SSL protocols](https://docs.microsoft.com/en-us/dotnet/api/system.security.authentication.sslprotocols?view=netcore-3.1) enabled on the given HTTP client. Protocol names are specified as array of strings. Default value is [None](https://docs.microsoft.com/en-us/dotnet/api/system.security.authentication.sslprotocols?view=netcore-3.1#System_Security_Authentication_SslProtocols_None).
```JSON
"SslProtocols": [
    "Tls11",
    "Tls12"
]
```
- MaxConnectionsPerServer - maximal number of HTTP 1.1 connections open concurrently to the same server. Default value is [int32.MaxValue](https://docs.microsoft.com/en-us/dotnet/api/system.int32.maxvalue?view=netcore-3.1).
```JSON
"MaxConnectionsPerServer": "10"
```
- DangerousAcceptAnyServerCertificate - indicates whether the server's SSL certificate validity is checked by the client. Setting it to `true` completely disables validation. Default value is `false`.
```JSON
"DangerousAcceptAnyServerCertificate": "true"
```
- ClientCertificate - specifies a client [X509Certificate](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificate?view=netcore-3.1) certificate used to authenticate client on the server. Default value is `null`. There are 3 supported certificate formats
    - PFX file and optional password
    - PEM file and the key with an optional password
    - Certificate subject, store and location as well as `AllowInvalid` flag indicating whether or not an invalid certificate accepted
```JSON
// PFX file
"ClientCertificate": {
    "Path": "my-client-cert.pfx",
    "Password": "1234abc"
}

// PEM file
"ClientCertificate": {
    "Path": "my-client-cert.pem",
    "KeyPath": "my-client-cert.key",
    "Password": "1234abc"
}

// Subject, store and location
"ClientCertificate": {
    "Subject": "MyClientCert",
    "Store": "AddressBook",
    "Location": "LocalMachine",
    "AllowInvalid": "true"
}

```
## Configuration example
The below example shows 2 samples of HTTP client configurations for `cluster1` and `cluster2`.

```JSON
{
    "Clusters": {
        "cluster1": {
            "LoadBalancing": {
                "Mode": "Random"
            },
            "HttpClient": {
                "SslProtocols": [
                    "Tls11",
                    "Tls12"
                ],
                "MaxConnectionsPerServer": "10",
                "DangerousAcceptAnyServerCertificate": "true"
            },
            "Destinations": {
                "cluster1/destination1": {
                    "Address": "https://localhost:10000/"
                },
                "cluster1/destination2": {
                    "Address": "http://localhost:10010/"
                }
            }
        },
        "cluster2": {
            "HttpClient": {
                "SslProtocols": [
                    "Tls12"
                ],
                "ClientCertificate": {
                    "Path": "my-client-cert.pem",
                    "KeyPath": "my-client-cert.key",
                    "Password": "1234abc"
                }
            },
            "Destinations": {
                "cluster2/destination1": {
                    "Address": "https://localhost:10001/"
                }
            }
        }
    }
}
```

## Code Configuration
HTTP client configuration abstraction constists of the only type [ProxyHttpClientOptions](xref:Microsoft.ReverseProxy.Abstractions.ProxyHttpClientOptions) defined as follows.
```C#
public sealed class ProxyHttpClientOptions
{
    public List<SslProtocols> SslProtocols { get; set; }

    public bool DangerousAcceptAnyServerCertificate { get; set; }

    public X509Certificate ClientCertificate { get; set; }

    public int? MaxConnectionsPerServer { get; set; }
}
```
Note that instead of defining certificate location as it was in [CertificateConfigData](xref:Microsoft.ReverseProxy.Configuration.Contract.CertificateConfigData) model, this type exposes a fully constructed [X509Certificate](xref:System.Security.Cryptography.X509Certificates.X509Certificate) certificate. Conversion from the configuration contract to the abstraction model is done by a [IProxyConfigProvider](xref:Microsoft.ReverseProxy.Service.IProxyConfigProvider) which loads a client certificate into memory.

## Custom IProxyHttpClientFactory
In case the direct control on a proxy HTTP client construction is necessary, the default [IProxyHttpClientFactory](xref:Microsoft.ReverseProxy.Service.Proxy.Infrastructure.IProxyHttpClientFactory) can be replaced with a custom one. In example, that custom logic can use [Cluster](xref:Microsoft.ReverseProxy.Abstractions.Cluster)'s metadata as an extra data source for [HttpMessageInvoker](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessageinvoker?view=netcore-3.1) configuration. However, it's still recommended for any custom factory to set the following [HttpMessageInvoker](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessageinvoker?view=netcore-3.1) properties to the same values as the default factory does in order to preserve a correct reverse proxy behavior.

```C#
new SocketsHttpHandler
{
    UseProxy = false,
    AllowAutoRedirect = false,
    AutomaticDecompression = DecompressionMethods.None,
    UseCookies = false
};
```