# YARP Direct Proxy Example

The YARP Proxy uses a pipeline architecture, with multiple stages. These include:
- Integrating with ASP.NET routing
- Mapping from a route to a cluster of destinations
- Filtering the destinations based on health status and affinity

Some customers who have previously built a custom proxy solution have implementations of these concepts, but are coming to YARP for its support of HTTP/2, gRPC and modern protocols that are more complex to implement than HTTP/1.1. 
They already have implementations of routing etc that work with their other infrastructure, that they don't want to have to replace or rewrite. 
The goal with YARP is to provide a highly customizable proxy - so its very componentized. To make it easier to integrate YARP into these scenarios, the component that proxies requests is exposed via the HttpProxy class, which can be called directly, and has few dependencies on the rest of YARP's infrastructure. 

The operation of the proxy can be thought of as:

```
+-------------------+           +-------------------+           +-------------------+
|      Client       |  ──(a)──► |      Proxy        |  ──(b)──► |    Destination    |
|                   | ◄──(d)──  |                   | ◄──(c)──  |                   |
+-------------------+           +-------------------+           +-------------------+
```

(a) and (b) show the *request* path, going from the client to the destination.
(c) and (d) show the *response* path, going from the destination back to the client.

Normal proxying comprises the following steps:

| \# | Step | Direction |
| -- | ---- | --------- |
| 1 | Disable ASP .NET Core limits for streaming requests | |
| 2 | Create outgoing HttpRequestMessage | |
| 3 | Setup copy of request body (background) | Client --► Proxy --► Destination |
| 4 | Copy request headers | Client --► Proxy --► Destination |
| 5 | Send the outgoing request using HttpMessageInvoker | Client --► Proxy --► Destination |
| 6 | Copy response status line | Client ◄-- Proxy ◄-- Destination |
| 7 | Copy response headers | Client ◄-- Proxy ◄-- Destination |
| 8.1 | Check for a 101 upgrade response, this takes care of WebSockets as well as any other upgradeable protocol. | |
| 8.1.1 | Upgrade client channel | Client ◄--- Proxy ◄--- Destination |
| 8.1.2 | Copy duplex streams and return | Client ◄--► Proxy ◄--► Destination |
| 8.2 | Copy (normal) response body | Client ◄-- Proxy ◄-- Destination |
| 9 | Copy response trailer headers and finish response | Client ◄-- Proxy ◄-- Destination |
| 10 | Wait for completion of step 2: copying request body | Client --► Proxy --► Destination |

To enable control over mapping request and response fields and headers between the client and destination, the HttpProxy.ProxyAsync method takes a HttpTransformer. Your implementation can modify the request url, method, protocol version, response status code, or decide which headers are copied, modify them, or insert additional headers as required.

**Note:** When using the HttpProxy class directly there are no transforms included by default, you have full control of the transforms in your HttpTransformer implementation. The alternate YARP pipeline model (see BasicYarpSample), has some [default header transforms](https://microsoft.github.io/reverse-proxy/articles/transforms.html), such as adding ```X-Forwarded-For``` and removing the original Host header.

## Files

The key functionality for this sample is all included in [Startup.cs](Startup.cs).
