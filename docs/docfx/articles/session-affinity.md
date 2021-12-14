# Session Affinity

Introduced: preview2
Updated: preview12

## Concept
Session affinity is a mechanism to bind (affinitize) a causally related request sequence to the destination that handled the first request when the load is balanced among several destinations. It is useful in scenarios where the most requests in a sequence work with the same data and the cost of data access differs for different nodes (destinations) handling requests. The most common example is a transient caching (e.g. in-memory) where the first request fetches data from a slower persistent storage into a fast local cache and the others work only with the cached data thus increasing throughput.

## Configuration
### Services and middleware registration
Session affinity services are registered in the DI container automatically by `AddReverseProxy()`. The middleware `UseSessionAffinity()` is included by default in the parameterless MapReverseProxy method. If you are customizing the proxy pipeline, place the first middleware **before** adding `UseLoadBalancing()`.

Example:
```C#
endpoints.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseSessionAffinity();
    proxyPipeline.UseLoadBalancing();
});
```

### Cluster configuration
Session affinity is configured per cluster according to the following configuration scheme.
```JSON
"ReverseProxy": {
    "Clusters": {
        "<cluster-name>": {
            "SessionAffinity": {
                "Enabled": "(true|false)", // defaults to 'false'
                "Policy": "(Cookie|CustomHeader)", // defaults to 'Cookie'
                "FailurePolicy": "(Redistribute|Return503)", // defaults to 'Redistribute'
                "AffinityKeyName": "Key1",
                "Cookie": {
                    "Domain": "localhost",
                    "Expiration": "03:00:00",
                    "HttpOnly": true,
                    "IsEssential": true,
                    "MaxAge": "1.00:00:00",
                    "Path": "mypath",
                    "SameSite": "Strict",
                    "SecurePolicy": "Always"
            }
        }
    }
}
```

### Policy-specific configuration
There is currently one policy-specific strongly-typed configuration section implemented.
- `SessionAffinityCookieConfig` exposes settings to customize cookie properties which will be used by `CookieSessionAffinityPolicy` for creating new affinity cookies. The properties can be JSON config as show above or in code as shown below:
```C#
new ClusterConfig
{
    ClusterId = "cluster1",
    SessionAffinity = new SessionAffinityConfig
    {
        Enabled = true,
        FailurePolicy = "Return503Error",
        Policy = "Cookie",
        AffinityKeyName = "Key1",
        Cookie = new SessionAffinityCookieConfig
        {
            Domain = "mydomain",
            Expiration = TimeSpan.FromHours(3),
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(1),
            Path = "mypath",
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
            SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
        }
    }    
}
```

## Affinity Key
Request to destination affinity is established via the affinity key identifying the target destination. That key can be stored on different request parts depending on the given session affinity implementation, but each request cannot have more than one such key. The exact key semantics is implementation dependent, in example the both of built-in `CookieSessionAffinityPolicy` and `CustomHeaderAffinityPolicy` are currently use `DestinationId` as the affinity key.

The current design doesn't require a key to uniquely identify the single affinitized destination. It's allowed to establish affinity to a destination group. In this case, the exact destination to handle the given request will be determined by the load balancer.

## Establishing a new affinity or resolution of the existed one
Once a request arrives and gets routed to a cluster with session affinity enabled, the proxy automatically decides whether a new affinity should be established or an existing one needs to be resolved based on the presence and validity of an affinity key on the request as follows:
1. **Request doesn't contain a key**. Resolution is skipped and a new affinity will be establish to the destination chosen by the load balancer

2. **Affinity key is found on the request and valid**. The affinity mechanism tries to find all healthy destinations matching to the key, and if it finds some it passes the request down the pipeline. If multiple matching destinations are found the load balancer is invoked to choose the single target destination. If only one matching destination is found the load balancer no-ops.

3. **Affinity key is invalid or no healthy affinitized destinations found**. It's treated as a failure to be handled by a failure policy explained below

If a new affinity was established for the request, the affinity key gets attached to a response where exact key representation and location depends on the implementation. Currently, there are two built-in policies storing the key on a cookie or custom header. Once the response gets delivered to the client, it's the client responsibility to attach the key to all following requests in the same session. Further, when the next request carrying the key arrives to the proxy, it resolves the existing affinity, but affinity key does not get again attached to the response. Thus, only the first response carries the affinity key.

There are two built-in affinity policys differing only in how the affinity key is stored on a request. The default policy is `Cookie`.
1. `Cookie` - stores the key as a cookie. It expects the request's key to be delivered as a cookie with configured name and sets the same cookie with `Set-Cookie` header on the first response in an affinitized sequence. This is implemented by `CookieSessionAffinityPolicy`. Cookie name must be explicitly set via `SessionAffinityConfig.AffinityKeyName` which is used by `CookieSessionAffinityPolicy` for this purpose. Other cookie's properties can be configured via `SessionAffinityCookieConfig`. **Important**: `AffinityKeyName` must be unique across all clusters with enabled session affinity to avoid conflicts.

2. `CustomHeader` - stores the key on a header. It expects the affinity key to be delivered in a custom header with configured name and sets the same header on the first response in an affinitized sequence. This is implemented by `CustomHeaderSessionAffinityPolicy`. The header name must be set via `SessionAffinityConfig.AffinityKeyName` which is used by `CustomHeaderSessionAffinityPolicy` for this purpose. **Important**: `AffinityKeyName` must be unique across all clusters with enabled session affinity to avoid conflicts.

## Affinity failure policy
If the affinity key cannot be decoded or no healthy destination found it's considered as a failure and an affinity failure policy is called to handle it. The policy has the full access to `HttpContext` and can send response to the client by itself. It returns a boolean value indicating whether the request processing can proceed down the pipeline or must be terminated.

There are two built-in failure policies.  The default is `Redistribute`.
1. `Redistribute` - tries to establish a new affinity to one of available healthy destinations by skipping the affinity lookup step and passing all healthy destination to the load balancer the same way it is done for a request without any affinity. Request processing continues. This is implemented by `RedistributeAffinityFailurePolicy`.

2. `Return503Error` - sends a `503` response back to the client and request processing is terminated. This is implemented by `Return503ErrorAffinityFailurePolicy`

## Request pipeline
The session affinity mechanisms are implemented by the services (mentioned above) and the two following middleware:
1. `SessionAffinityMiddleware` - coordinates the request's affinity resolution process. First, it calls a policy specified for the given cluster on `ClusterConfig.SessionAffinity.Policy` property. Then, it checks the affinity resolution status returned by the policy, and calls a failure handling policy set on `ClusterConfig.SessionAffinity.FailurePolicy` in case of failures. It must be added to the pipeline **before** the load balancer.

2. `AffinitizeTransform` - sets the key on the response if a new affinity has been established for the request. Otherwise, if the request follows an existing affinity, it does nothing. This is automatically added as a response transform.
