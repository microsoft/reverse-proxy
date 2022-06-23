# YARP Tunneling

## Introduction
While many organizations are moving their computing to the cloud, there are occasions where you need to be able to run some services in a local datacenter. The problem then is if you need to be able to communicate with those services from the cloud. Creating a VPN network connection to Azure or other cloud provider is possible, but usually involves a lot of red tape, and configuration.

If all that the cloud needs to access is resources that are exposed over http, then a simpler solution is to have a gateway that will route traffic to the remote resource. Outbound connections over http are not usually blocked, so having a on-prem gateway make an outbound connection to the cloud, is the easiest way to establish the route.

That is the principle of the gateway feature for YARP. You operate two instances of the YARP proxy service, configured as a tunnel. 

![Tunnel diagram](https://github.com/assets/95136/52d7491b-6e8a-4a2c-a51d-0734b3e41930)

In the on-prem data center, you run an instance of YARP, we'll call this the BackEnd proxy. This is configured with routes to the resources that should be externally accessible - only routes that are configured via this proxy will be exposed. The BackEnd proxy is configured to create a tunnel connection to the FrontEnd instance by specifying the connection URL and security details for the connection.

The instance in the cloud, we'll refer to as the FrontEnd, will be configured with a tunnel endpoint URL to be used by the on-prem proxy. The on-prem proxy will create a websocket connection to the tunnel endpoint, this will map the tunnel to a specific cluster. Routes can be directed to use the tunnel connection to the backend by using the cluster that is used for the tunnel.

## Tunnel Protocol

The tunnel will establish a Websockets connection between the BackEnd and the FrontEnd. The BackEnd will establish the connection so that it can more easily break through firewalls. Once the WS connection is created, it will be treated as a stream over which HTTP/2 traffic will be routed. HTTP/2 is used so that multiple simultaneous requests can be multiplexed over a single connection. The HTTP/2 protocol is only used between the two proxies, the connections either side can be any protocol that the proxy supports. This means we don't put any specific capability requirement on the destination servers. 

If the tunnel connection is broken, the BackEnd will attempt to reconnect to the FrontEnd:
- If the connection fails, then it will continue to reconnect every 30s until the connection is re-established.
- If the connection is refused with a 500 series error, then it will be retried at the next 30s timeout. 
- If the connection is refused with a 400 series error then further connections for that tunnel will not be made.

> Issue: Do we need an API for the tunnel? As its created from code on the BackEnd, the app could have additional logic for control over the duration. Does it have API for status, clean shutdown, etc.

> Issue: Will additional connections be created for scalability - H2 perf becomes limited after 100 simultaneous requests. How does the FrontEnd know to pair a second BackEnd connection?

The Front End should keep the WS connection alive by sending pings every 30s if there is no other traffic. This should be done at the WS layer.

## Moving pieces

| Location | Name | Description |
| --- | --- | --- |
| FrontEnd | EndPoint | The endpoint that the backend proxy will connect to to create a tunnel. |
| FrontEnd | Cluster | The cluster that will direct to backend proxy(ies) that have created tunnels. |
| FrontEnd | Routes | Routes need to be configured to route specific URLs to the tunnel, by using clusters that are a tunnel. | 
| BackEnd | Tunnel URL(s) | The URL(s) for the frontend endpoint that can be used to establish the tunnel. |
| BackEnd | Routes | The backend needs to have routes defined that will direct traffic to local resources. |

## FrontEnd 
The FrontEnd is the proxy that will be called by clients to be able to access resources via the BackEnd proxy. It will route traffic over a tunnel created using a WS connection from the BackEnd proxy. YARP needs a mechanism to know which requests will be routed via the tunnel. This will be achived by extending the existing cluster concept in YARP - The request to create a tunnel will specify the name of a cluster. Once the tunnel is established, it will be treated as a dynmamically created destination for the named cluster. Routes will not need to be changed, they will point at the cluster, and the tunnels will be used in the same way as destinations. 

Tunnel services must be enabled by the proxy server:

``` C#
builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddTunnelServices();
```

The FrontEnd needs to have a tunnel endpoint that the BackEnd will connect to. The endpoint should be parameterized to include the name of the cluster as part of the URL, and a callback that is used to validate the connection is approved:
 - Including the ClusterId in the URL enables the same endpoint mechanism to be used for multiple clusters. 
 - Using a callback for authentication enables whatever scheme the proxy author(s) wish to use.
   - Trying to encode specific auth schemes will invariably miss a scenario that is needed.
   - The samples that we produce should be based around client certs as it is a good way to manage secure shared secrets.

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

The FrontEnd should have configuration for routes that direct to a cluster that is for the tunnel. The cluster must be marked as IsTunnel to enable tunnel capability, and must *not* include other destinations. The cluster's destinations will be supplied dynamically by BackEnds creating tunnel connections.

In the following case it uses the Extensions feature to enable storing thumbprints for client certs that are used to authenticate tunnel connections. The Route will direct all traffic under the path `/OnPrem/*` to the tunnel.

``` json
{
    "ReverseProxy":
    {
        "Routes" : {
            "OnPrem" : {
                "Match" : {
                    "Path" : "/OnPrem/{**any}"
                },
                "ClusterId" : "MyTunnel1"
            }
        },
        "Clusters" : {
            "MyTunnel1" : {
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

To ensure scalability, multiple BackEnd proxy instances should be able to create tunnel connections for the same cluster. When that happens, the load balancing rules for the cluster should apply, and balance between the active tunnels. Similarly multiple FrontEnds can be used to reduce the problems with a single point of failure.

## BackEnd

The BackEnd instance is the proxy that will reside on the same network as the resources that should be exposed. The BackEnd will need to be able to connect to those resources, and also be able to create a WebSocket connection to the FrontEnd proxy server(s) via whatever firewalls are between them.

The BackEnd proxy is configured with routes and destinations that it wishes to expose to the front end. Security is maintained because only URLs matching its routes will be proxyable via it. This prevents attacks at the FrontEnd having arbitrary access to other resources on the BackEnd network - they need to be explicitly included in the BackEnd route table.

The outbound connection to the front end needs to be explicitly made for each tunnel that the backend wishes to create.

``` C#
builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var url = builder.Configuration["Tunnel:Url"]!; // Eg https://MyFrontEnd.MyCorp.com/tunnel/MyTunnel1

// Setup additional details for the connection, auth and headers
var tunnelOptions = new TunnelOptions(){
       TunnelClient = new SocketsHttpHandler(),
       ClientCertificates = new X509CertificateCollection { cert }
       };
tunnelOptions.Headers.Add("MyJWTToken", tokenString);

builder.WebHost.UseTunnelTransport(url, tunnelOptions);
```

**Issue** Do we need a callback for the tunnel for the client to validate the server, or do we rely on TLS and a valid server certificate that must match the URL used by the tunnel?

Note: Active health checks probably don't make sense to be performed against the Back End. Passive health checks will verify the overall condition of the tunnel.

## Scalability

In a large deployment, there needs to be the ability to have multiple FrontEnd and BackEnd proxies:
- If the FrontEnd receives multiple tunnel connections, then it should treat them as if the cluster has multiple destinations. The cluster can use the load balancing policy to select how it decides to route traffic to the BackEnd proxies.

> Note: The FrontEnd proxy will not be aware of the actual destinations that serve resources - each BackEnd should have its own cluster definition for the actual destinations, and so can include multiple servers for any route/cluster combination.

- A BackEnd proxy should be able to create tunnels to multiple FrontEnds. The tunnels can be to related FrontEnd proxies that are sharing the same load, or to FrontEnds in different cloud deployments. This enables the Front Ends to be very specific to particular deployments - and have constrained v-Lan configurations in the cloud. This limits the possibility for other connections to the proxy that may cause security issues.

## Authentication

The authentcation options for ASP.NET are diverse, and IT departments will likely have their own conditions on what is required to be able to secure a tunnel. So rather than trying to implement the combinatorial matrix of what customers could need, we should use a callback so that the proxy author can decide.

Samples should be created that show best practices using a secure mechanism such as client a certificate.

*Issue:* Does the BackEnd need additional mechanisms to validate the connection to the FrontEnd, or is TLS/SNI sufficient?

## Security

The purpose of the tunnel is to somewhat subvert security by creating a tunnel through the firewall that enables external requests to be made to destination servers on the BackEnd network. There are a number of mitigations that reduces the risk of this feature:

* No endpoints are exposed via the firewall - it does not expose any new endpoints that could act as attack vectors. The tunnel is an outbound connection made between the BackEnd and the FrontEnd.
* Traffic directed via the tunnel will need to have corresponding routes in the Back End configuration. Traffic will only be routed if there is a respective route and cluster configuration. Tunnel traffic can't specify arbitrary URLs that would be directed to a hostname not included in the backend route table configuration.
* Tunnel connections should only be over HTTPs

## Metrics

? What telemery and events are needed for this?

## Error conditions

| Condition | Description |
| --- | --- |
| No tunnel has connected | If the front end receives a request for a route that is backed by a tunnel and no tunnels have been created, then it should respond to those requests with a 502 "Bad Gateway" error|

## Web Transport

Web Transport is an interesting future protocol choice for the tunnel.


