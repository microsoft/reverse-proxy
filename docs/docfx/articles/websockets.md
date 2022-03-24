# Proxing WebSockets and SPDY

## Introduction

YARP enables proxying WebSocket and SPDY connections by default. This support works with both the [direct forwarding](direct-forwarding.md) and [full pipeline](getting-started.md) approaches.

[WebSockets](https://www.rfc-editor.org/rfc/rfc6455.html) is a bidirectional streaming protocol built on HTTP/1.1.

[SPDY](https://www.chromium.org/spdy/spdy-protocol/) is the precursor to HTTP/2 and is commonly used in Kubernetes environments.

## Upgrades

WebSockets and SPDY are built on top of HTTP/1.1 using a feature called [connection upgrades](https://datatracker.ietf.org/doc/html/rfc7230#section-6.7). YARP proxies the initial request, and if the destination server responds with `101 Switching Protocols`, upgrades the connection to an opaque, bidirectional stream using the new protocol. YARP does not allow upgrading to other protocols like HTTP/2 this way.

## HTTP/2

YARP and its underlying components (ASP.NET Core and HttpClient) do not support [WebSockets over HTTP/2](https://datatracker.ietf.org/doc/html/rfc8441). Even if YARP is configured to proxy requests to the destination using HTTP/2, it will detect WebSocket and SPDY requests and use HTTP/1.1 to proxy them.

See https://github.com/microsoft/reverse-proxy/issues/1375 for future work here.
