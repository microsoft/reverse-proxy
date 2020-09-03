# Proxy HTTP Client Configuration

Introduced: preview5

## Introduction
Each `Cluster` has a dedicated `HttpMessageInvoker` instance used to proxy request to its `Destination`s and `HttMessageInvoker`'s configuration is defined on per cluster level. On YARP startup, all `Cluster`s get new `HttpMessageInvoker` instances, however if later the `Cluster` configuration gets changed a new `HttpMessageInvoker` gets created only when `IProxyHttpClientFactory` detects a difference between an old and a new configuration affecting HTTP client parameters. The default `ProxyHttpClientFactory` creates a new `HttpMessageInvoker` when there changes eithes to `ProxyHttpClientOptions` or to `Cluster.Metadata`.

HTTP client configuration represented by slightly different models in the Contract and Abstractions layers.

## Configuration contract
HTTP client configuration contract consists of `Microsoft.ReverseProxy.Configuration.Contract.ProxyHttpClientData` and `Microsoft.ReverseProxy.Configuration.Contract.CertificateConfigData` types defining the following configuration schema. This types are focused on defining serializable configuration. The in-memory HTTP client configuration model is described below in the "Configuration abstractions" section.
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
- SslProtocols - SSL protocols enabled on the given HTTP client. Protocol names are specified as array of strings.
```JSON
"SslProtocols": [
    "Tls11",
    "Tls12"
]
```
- MaxConnectionsPerServer - maximal number of HTTP 1.1 connections open concurrently to the same server
```JSON
"MaxConnectionsPerServer": "10"
```
- ValidateRemoteCertificate - indicates whether the server's SSL certificate validity is checked by the client. Setting it to **false** completely disables validation.
```JSON
"ValidateRemoteCertificate": "false"
```
- ClientCertificate - specifies a client SSL certificate used to authenticate client on the server. There are 3 supported certificate formats
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
            "HttpClientOptions": {
                "SslProtocols": [
                    "Tls11",
                    "Tls12"
                ],
                "MaxConnectionsPerServer": "10",
                "ValidateRemoteCertificate": "false"
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
            "HttpClientOptions": {
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

## Configuration abstractions
HTTP client configuration abstraction constists of the only type `Microsoft.ReverseProxy.Abstractions.ProxyHttpClientOptions` defined as follows.
```C#
public sealed class ProxyHttpClientOptions
{
    public List<SslProtocols> SslProtocols { get; set; }

    public bool ValidateRemoteCertificate { get; set; }

    public X509Certificate ClientCertificate { get; set; }

    public int? MaxConnectionsPerServer { get; set; }
}
```
Note that instead of defining certificate location as it was in `Microsoft.ReverseProxy.Configuration.Contract.CertificateConfigData` model, this type exposes a fully constructed `X509Certificate` certificate. Conversion from the configuration contract to the abstraction model is done by a `IProxyConfigProvider` which loads a client certificate into memory.