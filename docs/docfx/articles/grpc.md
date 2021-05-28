# Proxing gRPC

## Introduction

[gRPC](https://grpc.io/) is a common protocol used to exchange messages between clients and servers. It's based on top of HTTP (mainly HTTP/2) and can be proxied through YARP. While YARP doesn't need to be aware of the message formats (proto files), you do need to make sure the right protocols are enabled for the incoming server and outgoing client.

## Configure the Incoming Server

gRPC requires HTTP/2 for most scenarios. HTTP/1.1 and HTTP/2 are enabled by default on ASP.NET Core servers but https (TLS) is required to negotiate HTTP/2. HTTP/2 over http (non-TLS) is only supported on Kestrel and requires specific settings.  For details see [here](https://docs.microsoft.com/aspnet/core/grpc/aspnetcore#server-options).

This shows configuring Kestrel to use HTTP/2 over http (non-TLS):
```json
{
  "Kestrel": {
    "Endpoints": {
      "http": {
        "Url": "http://localhost:5000",
        "Protocols": "Http2"
      }
    }
  }
}
```

## Configure the Outgoing Client

YARP automatically negotiates HTTP/1.1 or HTTP/2 for outgoing proxy requests, but only for https (TLS). HTTP/2 over http (non-TLS) requires additional settings. Note outgoing protocols are independent of incoming ones. E.g. https can be used for the incoming connection and http for the outgoing one, this is called TLS termination. See [here](proxyhttpclientconfig.md#httprequest) for configuration details.

This shows configuring the outgoing proxy request to use HTTP/2 over http. Note the `VersionPolicy` settings requires .NET 5.0:
```json
"cluster1": {
  "HttpRequest": {
    "Version": "2",
    "VersionPolicy": "RequestVersionExact"
  },
  "Destinations": {
    "cluster1/destination1": {
      "Address": "http://localhost:6000/"
    }
  }
},
```

## gRPC-Web

[gRPC-Web](https://grpc.io/docs/platforms/web/basics/) is a version of gRPC that's compatible with HTTP/1.1. It can be proxied by YARP's default configuration without any special considerations.
