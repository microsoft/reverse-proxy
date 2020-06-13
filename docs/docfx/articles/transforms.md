# Transforms

Introduced: preview2

## Introduction
When proxing a request it's common to modify parts of the request or response to adapt to the destination server's requirements or to flow additional data such as the client's original IP address. This process is implemented via Transforms. Types of transforms are defined globally for the application and then individual routes supply the parameters to enable and configure those transforms. The original request objects are not modified by these transforms, only the proxy requests.

## Defaults
The following transforms are enabled by default for all routes. They can be configured or disabled as shown later in this document.
- Host - Suppress the incoming request's Host header. The proxy request will default to the host name specified in the destination server address. See [RequestHeaderOriginalHost](#requestheaderoriginalhost) below.
- X-Forwarded-For - Appends the client's IP address to the X-Forwarded-For header. See [X-Forwarded](#x-forwarded) below.
- X-Forwarded-Proto - Appends the request's original scheme (http/https) to the X-Forwarded-Proto header. See [X-Forwarded](#x-forwarded) below.
- X-Forwarded-Host - Appends the request's original Host to the X-Forwarded-Host header. See [X-Forwarded](#x-forwarded) below.
- X-Forwarded-PathBase - Appends the request's original PathBase to the X-Forwarded-Proto header. See [X-Forwarded](#x-forwarded) below.

## Configuration
Transforms are defined on [ProxyRoute.Transforms](xref:Microsoft.ReverseProxy.Abstractions.ProxyRoute.Transforms) and can be bound from the `Routes` sections of the config file. As with other route properties, these can be modified and reloaded without restarting the proxy. A transform is configured using one or more key-value string pairs.

Here is an example of common transforms:
```JSON
{
  "ReverseProxy": {
    "Routes": [
      {
        "RouteId": "route1",
        "BackendId": "backend1",
        "Match": {
          "Host": "localhost"
        },
        "Transforms": [
          { "PathPrefix": "/apis" },
          {
            "RequestHeader": "header1",
            "Append": "bar"
          },
          {
            "ResponseHeader": "header2",
            "Append": "bar",
            "When": "Always"
          },
          { "ClientCert": "X-Client-Cert" },
          { "RequestHeadersCopy": "true" },
          { "RequestHeaderOriginalHost": "true" },
          {
            "X-Forwarded": "proto,host,for,pathbase",
            "Append": "true",
            "Prefix": "X-Forwarded-"
          }
        ]
      },
      {
        "RouteId": "route2",
        "BackendId": "backend1",
        "Match": {
          "Path": "/api/{plugin}/stuff/{*remainder}"
        },
        "Transforms": [
          { "PathPattern": "/foo/{plugin}/bar/{remainder}" }
        ]
      }
    ],
    "Backends": {
      "backend1": {
        "Destinations": {
          "backend1/destination1": {
            "Address": "https://localhost:10001/Path/Base"
          }
        }
      }
    }
  }
}
```

All configuration entries are treated as case-insensitive, though the destination server may treat the resulting values as case sensitive or insensitive such as the path.

Transforms fall into a few categories: request parameters, request headers, response headers, and response trailers. Request and response body transforms are not supported by YARP but you can write middleware to do this. Request trailers are not supported because they are not supported by the underlying HttpClient.

### Request Parameters

Request parameters include the request path, query, version, and method. In code these are represented by the [RequestParametersTransformContext](xref:Microsoft.ReverseProxy.Service.RuntimeModel.Transforms.RequestParametersTransformContext) object and processed by implementations of the abstract class [RequestParametersTransform](xref:Microsoft.ReverseProxy.Service.RuntimeModel.Transforms.RequestParametersTransform).

Notes:
- The proxy request scheme (http/https), authority, and path base, are taken from the destination server address (`https://localhost:10001/Path/Base` in the example above) and cannot be modified by transforms.
- The Host header can be overridden by transforms independent of the authority, see [Request Headers](#request-headers) below.
- The request's original PathBase property is not used when constructing the proxy request, see [X-Forwarded](#x-forwarded) under [Request Headers](#request-headers).

The following are built in transforms identified by their primary config key. These transforms are applied in the order they are specified in the route configuration.

#### PathPrefix

| Key | Value | Required |
|-----|-------|----------|
| PathPrefix | A path starting with a '/' | yes |

```JSON
{ "PathPrefix": "/prefix" }
```

This will prefix the request path with the given value. E.g. it will modify the proxy request path from `/request/path` to `/prefix/request/path`.

#### PathRemovePrefix

| Key | Value | Required |
|-----|-------|----------|
| PathRemovePrefix | A path starting with a '/' | yes |

```JSON
{ "PathRemovePrefix": "/prefix" }
```

This will remove the matching prefix from the request path. E.g. it will modify the proxy request path from `/prefix/request/path` to `/request/path`. Matches are made on path segment boundaries (`/`). If the prefix does not match then no changes are made.

#### PathSet

| Key | Value | Required |
|-----|-------|----------|
| PathSet | A path starting with a '/' | yes |

```JSON
{ "PathSet": "/newpath" }
```

This will set the request path with the given value. E.g. it will modify the proxy request path from `/request/path` to `/newpath`.


#### PathPattern

| Key | Value | Required |
|-----|-------|----------|
| PathPattern | A path template starting with a '/' | yes |

```JSON
{ "PathPattern": "/my/{plugin}/api/{remainder}" }
```

This will set the request path with the given value and replace any `{}` segments with the associated route value. `{}` segments without a matching route value are removed. See ASP.NET Core's [routing docs](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-3.1#route-template-reference) for more information about route templates.

Example:

| Step | Value |
|------|-------|
| Route definition | `/api/{plugin}/stuff/{*remainder}` |
| Request path | `/api/v1/stuff/more/stuff` |
| Plugin value | `v1` |
| Remainder value | `more/stuff` |
| PathPattern | `/my/{plugin}/api/{remainder}` |
| Result | `/my/v1/api/more/stuff` |


### Request Headers

All incoming request headers are copied to the proxy request by default with the exception of the Host header (see [Defaults](#defaults)). [X-Forwarded](#x-forwarded) headers are also added by default. These behaviors can be configured using the following transforms. Additional request headers can be specified, or request headers can be excluded by setting them to an empty value.

In code these are implemented as derivations of the abstract class [RequestHeaderTransform](xref:Microsoft.ReverseProxy.Service.RuntimeModel.Transforms.RequestHeaderTransform).

Only one transform per header name is supported.

#### RequestHeadersCopy

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| RequestHeadersCopy | true/false | true | yes |

```JSON
{ "RequestHeadersCopy": "false" }
```

This sets if all incoming request headers are copied to the proxy request. This setting is enabled by default and can by disabled by configuring the transform with a `false` value. Transforms that reference specific headers will still be run if this is disabled.

#### RequestHeaderOriginalHost

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| RequestHeaderOriginalHost | true/false | false | yes |

```JSON
{ "RequestHeaderOriginalHost": "false" }
```

This specifies if the incoming request Host header should be copied to the proxy request. This setting is disabled by default and can be enabled by configuring the transform with a `true` value. Transforms that directly reference the `Host` header will override this transform.

#### RequestHeader

| Key | Value | Required |
|-----|-------|----------|
| RequestHeader | The header name | yes |
| Set/Append | The header value | yes |

```JSON
{
  "RequestHeader": "MyHeader",
  "Set": "MyValue",
}
```

This sets or appends the value for the named header. Set replaces any existing header. Set a header to empty to remove it (e.g. `"Set": ""`). Append adds an additional header with the given value.

#### X-Forwarded

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| X-Forwarded | A comma separated list containing any of these values: for,proto,host,PathBase | "for,proto,host,PathBase" | yes |
| Prefix | The header name prefix | "X-Forwarded-" | no |
| Append | true/false | true | no |

```JSON
{
  "X-Forwarded": "for,proto,host,PathBase",
  "Prefix": "X-Forwarded-",
  "Append": "true"
}
```

X-Forwarded-* headers are a common way to forward information to the destination server that may otherwise be obscured by the use of a proxy. The destination server likely needs this information for security checks and to properly generate absolute URIs for links and redirects. There is no standard that defines these headers and implementations vary, check your destination server for support. 

This transform is enabled by default even if not specified in the route config.

Set the `X-Forwarded` value to a comma separated list containing the headers you need to enable. All for headers are enabled by default. All can be disabled by specifying an empty value `""`.

The Prefix specifies the header name prefix to use for each header. With the default `X-Forwarded-` prefix the resulting headers will be `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Forwarded-PathBase`.

Append specifies if each header should append to or replace an existing header of the same name. A request traversing multiple proxies may accumulate a list of such headers and the destination server will need to evaluate the list to determine the original value. If append is false and the associated value is not available on the request (e.g. RemoteIpAddress is null), any existing header is still removed to prevent spoofing.

The {Prefix}For header value is taken from `HttpContext.Connection.RemoteIpAddress` representing the prior caller's IP address. The port is not included. IPv6 addresses do not include the bounding `[]` brackets.

The {Prefix}Proto header value is taken from `HttpContext.Request.Scheme` indicating if the caller used HTTP or HTTPS.

The {Prefix}Host header value is taken from the incoming request's Host header. This is independent of RequestHeaderOriginalHost specified above. Unicode/IDN hosts are punycode encoded.

The {Prefix}PathBase header value is taken from `HttpContext.Request.PathBase`. The PathBase property is not used when generating the proxy request so the destination server will need the original value to correctly generate links and directs. The value is in the percent encoded Uri format.

#### ClientCert

| Key | Value | Required |
|-----|-------|----------|
| ClientCert | The header name | yes |

```JSON
{ "ClientCert": "X-Client-Cert" }
```

This transform causes the client certificate taken from `HttpContext.Connection.ClientCertificate` to be Base64 encoded and set as the value for the given header name. This is needed because client certificates from incoming connections are not used when making connections to the destination server. The destination server may need that certificate to authenticate the client. There is no standard that defines this header and implementations vary, check your destination server for support.

### Response Headers and Trailers

All response headers and trailers are copied from the proxied response to the outgoing response. Response header and trailer transforms may specify if they should be applied only for successful responses of for all responses.

In code these are implemented as derivations of the abstract class [ResponseHeaderTransform](xref:Microsoft.ReverseProxy.Service.RuntimeModel.Transforms.ResponseHeaderTransform).

#### ResponseHeader

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| ResponseHeader | The header name | (none) | yes |
| Set/Append | The header value | (none) | yes |
| When | Success/Always | Success | no |

```JSON
{
  "ResponseHeader": "HeaderName",
  "Append": "value",
  "When": "Success"
}
```

This sets or appends the value for the named header. Set replaces any existing header. Set a header to empty to remove it (e.g. `"Set": ""`). Append adds an additional header with the given value.

`When` specifies if the response header should be included for successful responses or for all responses. Any response with a status code less than 400 is considered a success.

#### ResponseTrailer

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| ResponseTrailer | The header name | (none) | yes |
| Set/Append | The header value | (none) | yes |
| When | Success/Always | Success | no |

```JSON
{
  "ResponseTrailer": "HeaderName",
  "Append": "value",
  "When": "Success"
}
```

Response trailers are headers sent at the end of the response body. Support for trailers is uncommon in HTTP/1.1 implementations but is becoming common in HTTP/2 implementations. Check your client and server for support.

ResponseTrailer follows the same structure and guidance as ResponseHeader.

## Extensibility

To be continued, see [#60](https://github.com/microsoft/reverse-proxy/issues/60).
