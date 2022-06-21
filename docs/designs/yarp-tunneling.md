# YARP Tunneling

## Introduction
While many organizations are moving their computing to the cloud, there are occasions where you need to be able to run some services in a local datacenter. The problem then is if you need to be able to communicate with those services from the cloud. Creating a VPN network connection to Azure or other cloud provider is possible, but usually involves a lot of red tape, and configuration.

If all that the cloud needs to access is resources that are exposed over http, then a simpler solution is to have a gateway that will route traffic to the remote resource. Outbound connections over http are not usually blocked, so having a on-prem gateway make an outbound connection to the cloud, is the easiest way to establish the route.

That is the principle of the gateway feature for YARP. You operate two instances of the YARP proxy service, configured as a tunnel. 

![Tunnel diagram](https://github.com/assets/95136/52d7491b-6e8a-4a2c-a51d-0734b3e41930)

In the on-prem data center, you run an instance of YARP, we'll call this the backend proxy. This is configured with routes to the resources that should be externally accessible - only routes that are configured via this proxy will be exposed. The backend proxy is configured to create a tunnel connection to the frontend instance by specifying the connection URL and security details for the connection.

The instance in the cloud, we'll refer to as the frontend, will be configured with a tunnel endpoint URL to be used by the on-prem proxy. The on-prem proxy will create a websocket connection to the tunnel endpoint, this will map the tunnel to a specific cluster. Routes can be directed to use the tunnel connection to the backend by using the cluster that is the tunnel.

## Tunnel Protocol

The tunnel will establish a Websockets connection between the backend and the frontend. The backend will establish the connection so that it can more easily break through firewalls. Once the WS connection is created, it will be treated as a stream over which HTTP/2 traffic will be routed. HTTP/2 is used so that multiple simultaneous requests can be multiplexed over a single connection. The HTTP/2 protocol is only used between the two proxies, the connections either side can be any protocol that the proxy supports. This means we don't put any specific capability requirement on the destination servers. 

If the tunnel connection is broken, the Backend will attempt to reconnect to the front end. If the connection fails, then it will continue to reconnect every 30s until the connection is re-established. If the connection is refused with a 400 series error then further connections for that tunnel will not be made.

> Issue: Do we need an API for the tunnel? As its created from code on the Back End, the app could have additional logic for control over the duration. Does it have API for status, clean shutdown, etc.

The Front End should keep the WS connection alive by sending pings every 30s if there is not other traffic.

## Moving pieces

| Location | Name | Description |
| --- | --- | --- |
| Frontend | EndPoint | The endpoint that the backend proxy will connect to to create a tunnel. |
| Frontend | Cluster | The cluster that will direct to backend proxy(ies) that have created tunnels. |
| Frontend | Routes | Routes need to be configured to route specific URLs to the tunnel, by using clusters that are a tunnel. | 
| Backend | Tunnel URL(s) | The URL(s) for the frontend endpoint that can be used to establish the tunnel. |
| Backend | Routes | The backend needs to have routes defined that will direct traffic to local resources. |

## Front End 
The Front End is the proxy that will be called by clients to be able to access resources via the Back End proxy. It will route traffic over a WS connection from the Back End proxy.

Tunnel services must be enabled by the proxy server.

``` C#
builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddTunnelServices();
```

The front end needs to have a tunnel endpoint that the backend will connect to. The endpoint should specify the name of the cluster as part of the URL, and a callback that is used to validate the connection is approved:
 - Including the ClusterId in the URL enables the mechanism to be used for multiple clusters. It shouldn't be necessary for a single destination to be part of multiple clusters.
 - Using a callback for authentication enables whatever scheme the proxy author(s) wish to use. The samples that we produce should be based around client certs as its the easiest way to have some form of secure shared secrets.

``` C#
app.MapReversProxy();
app.MapTunnel("/tunnel/{ClusterId}", async (connectionContext, cluster) => {

    // Use the extensions feature https://github.com/microsoft/reverse-proxy/issues/1709 to add auth data for the tunnel
    var tunnelAuth = cluster.Extensions[typeof(TunnelAuth)]; 
    if (!context.Connection.ClientCertificate.Verify()) return false;
    foreach (var c in tunnelAuth.Certs)
    {
        if (c.ThumbPrint == context.Connection.ClientCertificate.Thumbprint) return true;
    }
    return false;
});

```

The frontend should have configuration for routes that direct to a cluster that is for the tunnel. The cluster must be marked as IsTunnel to enable tunnel capability, and must *not* include other destinations. All the destinations will be supplied dynamically by Back Ends creating tunnel connections.

In the following case it uses the extensions feature to enable storing thumbprints for client certs that are valid for that tunnel connection. The Route will direct all traffic under the path `/OnPrem/*` to the tunnel.

``` json
{
    "ReverseProxy":
    {
        "Routes" : {
            "Tunnel1" : {
                "Match" : {
                    "Path" : "/OnPrem/{**any}"
                },
                "ClusterId" : "TunnelDestinations"
            }
        },
        "Clusters" : {
            "TunnelDestinations" : {
                "IsTunnel" : true,
                "Extensions" : {
                    "TunnelAuth" : {
                        "Certs" : {
                            "name1" : "thumbprint1",
                            "name2" : "thumbprint2"
                        }
                    }
                }
            }
        }
    }
}
```

Multiple Back Ends should be able to create tunnel connections for the same cluster. When that happens, the load balancing rules for the cluster should apply, and balance between the active tunnels. The reason for this is scalability so that there is not a single point of failure.

## Back End

The Back End instance is the proxy that will reside on the same network as the resources that should be exposed. The Back End will need to be able to connect to those resources, and also be able to create a WebSocket connection to the Front End server(s).

The backend proxy is configured with routes and destinations that it wishes to expose to the front end. Only URLs matching its routes should be proxyable via it, so that configuration is critical to the security of the tunnel.

The outbound connection to the front end needs to be explicitly made for each tunnel that the backend wishes to create.

``` C#
builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var url = builder.Configuration["Tunnel:Url"]!;

// Setup additional details for the connection, auth and headers
var tunnelClient = new SocketsHttpHandler();
tunnelClient.SslOptions.ClientCertificates = new X509CertificateCollection { cert };
var headers = new Dictionary<string,string>();

builder.WebHost.UseTunnelTransport(url, tunnelClient, headers);

```

**Issue** Do we need a callback for the tunnel for the client to validate the server, or do we rely on TLS and a valid server certificate that must match the URL used by the tunnel?

Active health checks probably don't make sense to be performed against the Back End.

## Scalability

In a large deployment, there needs to be the ability to have multiple Front End and Back End proxies:
- If the Front End receives multiple tunnel connections, then it should treat them as if the cluster has multiple destinations. The cluster can use the load balancing policy to select how it decides to route traffic to the Back End proxies.

> Note: The Front End proxy will not be aware of the actual destinations that serve resources - a single Back End should have its own cluster definition for the actual destinations, and so can include multiple servers for any route/cluster combination.

- A Back End proxy should be able to create tunnels to multiple Front Ends. The tunnels can be to related servers that are sharing the same load, or to front ends in different cloud deployments. This enables the Front Ends to be very specific to particular deployments - and have constrained v-Lan configurations in the cloud. This limits the possibility for other connections to the proxy that may cause issues.

## Authentication

The authentcation options for ASP.NET are diverse, and IT departments will likely have their own conditions on what is required to be able to secure a tunnel. So rather than trying to implement the combinatorial matrix of what customers could need, we should use a callback so that the proxy author can decide.

Samples should be created that show best practices using a secure mechanism such as client a certificate.

## Security

The purpose of the tunnel is to somewhat subvert security by creating a tunnel through the firewall that enables external requests to be made to destination servers on the back end network. There are a number of mitigations that reduces the risk of this feature:

* No endpoints are exposed via the firewall - it does not expose any new endpoints that could act as attack vectors. The tunnel is an outbound connection made between the Back End and the Front End.
* Traffic directed via the tunnel will need to have corresponding routes in the Back End configuration. Traffic will only be routed if there is a respective route and cluster configuration. Tunnel traffic can't specify arbitrary URLs that would be directed to a hostname not included in the backend route table configuration.
* Tunnel connections should only be over HTTPs

## Metrics

? What telemery and events are needed for this?

## Error conditions

| Condition | Description |
| --- | --- |
| No tunnel has connected | If the front end receives a request for a route that is backed by a tunnel and no tunnels have been created, then it should respond to those requests with a 502 "Bad Gateway" error|




