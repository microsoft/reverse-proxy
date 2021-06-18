# Transforms

Introduced: preview2

## Introduction
When proxying a request it's common to modify parts of the request or response to adapt to the destination server's requirements or to flow additional data such as the client's original IP address. This process is implemented via Transforms. Types of transforms are defined globally for the application and then individual routes supply the parameters to enable and configure those transforms. The original request objects are not modified by these transforms, only the proxy requests.

## Defaults
The following transforms are enabled by default for all routes. They can be configured or disabled as shown later in this document.
- Host - Suppress the incoming request's Host header. The proxy request will default to the host name specified in the destination server address. See [RequestHeaderOriginalHost](#requestheaderoriginalhost) below.
- X-Forwarded-For - Appends the client's IP address to the X-Forwarded-For header. See [X-Forwarded](#x-forwarded) below.
- X-Forwarded-Proto - Appends the request's original scheme (http/https) to the X-Forwarded-Proto header. See [X-Forwarded](#x-forwarded) below.
- X-Forwarded-Host - Appends the request's original Host to the X-Forwarded-Host header. See [X-Forwarded](#x-forwarded) below.
- X-Forwarded-Prefix - Appends the request's original PathBase, if any, to the X-Forwarded-Prefix header. See [X-Forwarded](#x-forwarded) below.

For example the following incoming request to `http://IncomingHost:5000/path`:
```
GET /path HTTP/1.1
Host: IncomingHost:5000
Accept: */*
header1: foo
```
would be transformed and proxied to the destination server `https://DestinationHost:6000/` as follows using these defaults:
```
GET /path HTTP/1.1
Host: DestinationHost:6000
Accept: */*
header1: foo
X-Forwarded-For: 5.5.5.5
X-Forwarded-Proto: http
X-Forwarded-Host: IncomingHost:5000
```

Transforms fall into a few categories: request, response, and response trailers. Request and response body transforms are not supported by YARP but you can write middleware to do this. Request trailers are not supported because they are not supported by the underlying HttpClient.


Transforms can be added to routes either through configuration or programmatically.

## From Configuration

Transforms can be configured on [RouteConfig.Transforms](xref:Yarp.ReverseProxy.Configuration.RouteConfig) and can be bound from the `Routes` sections of the config file. These can be modified and reloaded without restarting the proxy. A transform is configured using one or more key-value string pairs.

Here is an example of common transforms:
```JSON
{
  "ReverseProxy": {
    "Routes": {
      "route1" : {
        "ClusterId": "cluster1",
        "Match": {
          "Hosts": [ "localhost" ]
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
            "X-Forwarded": "proto,host,for,prefix",
            "Append": "true",
            "Prefix": "X-Forwarded-"
          }
        ]
      },
      "route2" : {
        "ClusterId": "cluster1",
        "Match": {
          "Path": "/api/{plugin}/stuff/{*remainder}"
        },
        "Transforms": [
          { "PathPattern": "/foo/{plugin}/bar/{remainder}" },
          {
            "QueryStringParameter": "q",
            "Append": "plugin"
          }
        ]
      }
    },
    "Clusters": {
      "cluster1": {
        "Destinations": {
          "cluster1/destination1": {
            "Address": "https://localhost:10001/Path/Base"
          }
        }
      }
    }
  }
}
```

All configuration entries are treated as case-insensitive, though the destination server may treat the resulting values as case sensitive or insensitive such as the path.

The details for these transforms are covered later in this document.

Developers that want to integrate their custom transforms with the `Transforms` section of configuration can do so using [ITransformFactory](#itransformfactory) described below.

## From Code

Transforms can be added to routes programmatically by calling the [AddTransforms](xref:Microsoft.Extensions.DependencyInjection.ReverseProxyServiceCollectionExtensions) method.

`AddTransforms` can be called from `Startup.ConfigureServices` to provide a callback for configuring transforms. This callback is invoked each time a route is built or rebuilt and allows the developer to inspect the [RouteConfig](xref:Yarp.ReverseProxy.Abstractions.RouteConfig) information and conditionally add transforms for it.

The `AddTransforms` callback provides a [TransformBuilderContext](xref:Yarp.ReverseProxy.Transforms.Builder.TransformBuilderContext) where transforms can be added or configured. Most transforms provide `TransformBuilderContext` extension methods to make them easier to add. These are extensions documented below with the individual transform descriptions.

The `TransformBuilderContext` also includes an `IServiceProvider` for access to any needed services.

```C#
services.AddReverseProxy()
    .LoadFromConfig(_configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        // Added to all routes.
        builderContext.AddPathPrefix("/prefix");

        // Conditionally add a transform for routes that require auth.
        if (!string.IsNullOrEmpty(builderContext.Route.AuthorizationPolicy))
        {
            builderContext.AddRequestTransform(async transformContext =>
            {
                transformContext.ProxyRequest.Headers.Add("CustomHeader", "CustomValue");
            });
        }
    });
```

For more advanced control see [ITransformProvider](#itransformprovider) described below.

## Request transforms

Request transforms include the request path, query, HTTP version, method, and headers. In code these are represented by the [RequestTransformContext](xref:Yarp.ReverseProxy.Transforms.RequestTransformContext) object and processed by implementations of the abstract class [RequestTransform](xref:Yarp.ReverseProxy.Transforms.RequestTransform).

Notes:
- The proxy request scheme (http/https), authority, and path base, are taken from the destination server address (`https://localhost:10001/Path/Base` in the example above) and should not be modified by transforms.
- The Host header can be overridden by transforms independent of the authority, see [RequestHeader](#requestheader) below.
- The request's original PathBase property is not used when constructing the proxy request, see [X-Forwarded](#x-forwarded).
- All incoming request headers are copied to the proxy request by default with the exception of the Host header (see [Defaults](#defaults)). [X-Forwarded](#x-forwarded) headers are also added by default. These behaviors can be configured using the following transforms. Additional request headers can be specified, or request headers can be excluded by setting them to an empty value.

The following are built in transforms identified by their primary config key. These transforms are applied in the order they are specified in the route configuration.

### PathPrefix

| Key | Value | Required |
|-----|-------|----------|
| PathPrefix | A path starting with a '/' | yes |

Config:
```JSON
{ "PathPrefix": "/prefix" }
```
Code:
```csharp
routeConfig = routeConfig.WithTransformPathPrefix(prefix: "/prefix");
```
```C#
transformBuilderContext.AddPathPrefix(prefix: "/prefix");
```
Example:<br/>
`/request/path` becomes `/prefix/request/path`

This will prefix the request path with the given value.

### PathRemovePrefix

| Key | Value | Required |
|-----|-------|----------|
| PathRemovePrefix | A path starting with a '/' | yes |

Config:
```JSON
{ "PathRemovePrefix": "/prefix" }
```
Code:
```csharp
routeConfig = routeConfig.WithTransformPathRemovePrefix(prefix: "/prefix");
```
```csharp
transformBuilderContext.AddPathRemovePrefix(prefix: "/prefix");
```
Example:<br/>
`/prefix/request/path` becomes `/request/path`<br/>
`/prefix2/request/path` is not modified<br/>

This will remove the matching prefix from the request path. Matches are made on path segment boundaries (`/`). If the prefix does not match then no changes are made.

### PathSet

| Key | Value | Required |
|-----|-------|----------|
| PathSet | A path starting with a '/' | yes |

Config:
```JSON
{ "PathSet": "/newpath" }
```
Code:
```csharp
routeConfig = routeConfig.WithTransformPathSet(path: "/newpath");
```
```C#
transformBuilderContext.AddPathSet(path: "/newpath");
```
Example:<br/>
`/request/path` becomes `/newpath`

This will set the request path with the given value.

### PathPattern

| Key | Value | Required |
|-----|-------|----------|
| PathPattern | A path template starting with a '/' | yes |

Config:
```JSON
{ "PathPattern": "/my/{plugin}/api/{remainder}" }
```
Code:
```csharp
routeConfig = routeConfig.WithTransformPathRouteValues(pattern: new PathString("/my/{plugin}/api/{remainder}"));
```
```C#
transformBuilderContext.AddPathRouteValues(pattern: new PathString("/my/{plugin}/api/{remainder}"));
```

This will set the request path with the given value and replace any `{}` segments with the associated route value. `{}` segments without a matching route value are removed. See ASP.NET Core's [routing docs](https://docs.microsoft.com/aspnet/core/fundamentals/routing#route-template-reference) for more information about route templates.

Example:

| Step | Value |
|------|-------|
| Route definition | `/api/{plugin}/stuff/{*remainder}` |
| Request path | `/api/v1/stuff/more/stuff` |
| Plugin value | `v1` |
| Remainder value | `more/stuff` |
| PathPattern | `/my/{plugin}/api/{remainder}` |
| Result | `/my/v1/api/more/stuff` |

### QueryValueParameter

| Key | Value | Required |
|-----|-------|----------|
| QueryValueParameter | Name of a query string parameter | yes |
| Set/Append | Static value | yes |

Config:
```JSON
{
  "QueryValueParameter": "foo",
  "Append": "bar"
}
```
Code:
```csharp
routeConfig = routeConfig.WithTransformQueryValue(queryKey: "foo", value: "bar", append: true);
```
```C#
transformBuilderContext.AddQueryValue(queryKey: "foo", value: "bar", append: true);
```

This will add a query string parameter with the name `foo` and sets it to the static value `bar`.

Example:

| Step | Value |
|------|-------|
| Query | `?a=b` |
| QueryValueParameter | `foo` |
| Append | `remainder` |
| Result | `?a=b&foo=remainder` |

### QueryRouteParameter

| Key | Value | Required |
|-----|-------|----------|
| QueryRouteParameter | Name of a query string parameter | yes |
| Set/Append | The name of a route value | yes |

Config:
```JSON
{
  "QueryRouteParameter": "foo",
  "Append": "remainder"
}
```
Code:
```csharp
routeConfig = routeConfig.WithTransformQueryRouteValue(queryKey: "foo", routeValueKey: "remainder", append: true);
```
```C#
transformBuilderContext.AddQueryRouteValue(queryKey: "foo", routeValueKey: "remainder", append: true);
```

This will add a query string parameter with the name `foo` and sets it to the value of the associated route value.

Example:

| Step | Value |
|------|-------|
| Route definition | `/api/{*remainder}` |
| Request path | `/api/more/stuff` |
| Remainder value | `more/stuff` |
| QueryRouteParameter | `foo` |
| Append | `remainder` |
| Result | `?foo=more/stuff` |

### QueryRemoveParameter

| Key | Value | Required |
|-----|-------|----------|
| QueryRemoveParameter | Name of a query string parameter | yes |

Config:
```JSON
{ "QueryRemoveParameter": "foo" }
```
Code:
```csharp
routeConfig = routeConfig.WithTransformQueryRemoveKey(queryKey: "foo");
```
```C#
transformBuilderContext.AddQueryRemoveKey(queryKey: "foo");
```

This will remove a query string parameter with the name `foo` if present on the request.

Example:

| Step | Value |
|------|-------|
| Request path | `?a=b&foo=c` |
| QueryRemoveParameter | `foo` |
| Result | `?a=b` |

### HttpMethod

| Key | Value | Required |
|-----|-------|----------|
| HttpMethod | The http method to replace | yes |
| Set | The new http method | yes |

Config:
```JSON
{
  "HttpMethod": "PUT",
  "Set": "POST",
}
```
Code:
```csharp
routeConfig = routeConfig.WithTransformHttpMethodChange(fromHttpMethod: HttpMethods.Put, toHttpMethod: HttpMethods.Post);
```
```C#
transformBuilderContext.AddHttpMethodChange(fromHttpMethod: HttpMethods.Put, toHttpMethod: HttpMethods.Post);
```

This will change PUT requests to POST.

### RequestHeadersCopy

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| RequestHeadersCopy | true/false | true | yes |

Config:
```JSON
{ "RequestHeadersCopy": "false" }
```
Code:
```csharp
routeConfig = routeConfig.WithTransformCopyRequestHeaders(copy: false);
```
```C#
transformBuilderContext.CopyRequestHeaders = false;
```

This sets if all incoming request headers are copied to the proxy request. This setting is enabled by default and can by disabled by configuring the transform with a `false` value. Transforms that reference specific headers will still be run if this is disabled.

### RequestHeaderOriginalHost

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| RequestHeaderOriginalHost | true/false | false | yes |

Config:
```JSON
{ "RequestHeaderOriginalHost": "true" }
```
```csharp
routeConfig = routeConfig.WithTransformUseOriginalHostHeader(useOriginal: true);
```
```C#
transformBuilderContext.UseOriginalHost = true;
```

This specifies if the incoming request Host header should be copied to the proxy request. This setting is disabled by default and can be enabled by configuring the transform with a `true` value. Transforms that directly reference the `Host` header will override this transform.

### RequestHeader

| Key | Value | Required |
|-----|-------|----------|
| RequestHeader | The header name | yes |
| Set/Append | The header value | yes |

Config:
```JSON
{
  "RequestHeader": "MyHeader",
  "Set": "MyValue",
}
```
Code:
```csharp
routeConfig = routeConfig.WithTransformRequestHeader(headerName: "MyHeader", value: "MyValue", append: false);
```
```C#
transformBuilderContext.AddRequestHeader(headerName: "MyHeader", value: "MyValue", append: false);
```

Example:
```
MyHeader: MyValue
```

This sets or appends the value for the named header. Set replaces any existing header. Append adds an additional header with the given value.
Note: setting "" as a header value is not recommended and can cause an undefined behavior.

### RequestHeaderRemove

| Key | Value | Required |
|-----|-------|----------|
| RequestHeaderRemove | The header name | yes |

Config:
```JSON
{
  "RequestHeaderRemove": "MyHeader"
}
```
Code:
```csharp
routeConfig = routeConfig.WithTransformRequestHeaderRemove(headerName: "MyHeader");
```
```C#
transformBuilderContext.AddRequestHeaderRemove(headerName: "MyHeader");
```

Example:
```
MyHeader: MyValue
AnotherHeader: AnotherValue
```

This removes the named header.

### X-Forwarded

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| X-Forwarded | Default action (Set, Append, Remove, Off) to apply to all X-Forwarded-* listed below | Set | no |
| X-Forwarded-For | Action to apply to this header | Set | no |
| X-Forwarded-Proto | Action to apply to this header | Set | no |
| X-Forwarded-Host | Action to apply to this header | Set | no |
| X-Forwarded-Prefix | Action to apply to this header | Set | no |
| Prefix | The header name prefix | "X-Forwarded-" | no |

Action "Off" completely disables the transform.

Config:
```JSON
{
  "X-Forwarded": "Set",
  "X-Forwarded-For": "Remove",
  "X-Forwarded-Proto": "Append",
  "X-Forwarded-Prefix": "Off",
  "Prefix": "X-Forwarded-"
}
```
Code:
```csharp
routeConfig = routeConfig.WithTransformXForwarded(headerPrefix: "X-Forwarded-", useFor: true, useHost: true, useProto: true, usePrefix: true, action: ForwardedTransformAction.Remove);
```
```C#
transformBuilderContext.AddXForwarded(headerPrefix: "X-Forwarded-", ForwardedTransformAction.Remove);
transformBuilderContext.AddXForwardedFor(headerPrefix: "X-Forwarded-", ForwardedTransformAction.Remove);
transformBuilderContext.AddXForwardedHost(headerPrefix: "X-Forwarded-", ForwardedTransformAction.Remove);
transformBuilderContext.AddXForwardedProto(headerPrefix: "X-Forwarded-", ForwardedTransformAction.Remove);
```

Example:
```
X-Forwarded-For: 5.5.5.5
X-Forwarded-Proto: https
X-Forwarded-Host: IncomingHost:5000
X-Forwarded-Prefix: /path/base
```
Disable default headers:
```JSON
{ "X-Forwarded": "" }
```
```C#
transformBuilderContext.UseDefaultForwarders = false;
```

X-Forwarded-* headers are a common way to forward information to the destination server that may otherwise be obscured by the use of a proxy. The destination server likely needs this information for security checks and to properly generate absolute URIs for links and redirects. There is no standard that defines these headers and implementations vary, check your destination server for support.

This transform is enabled by default even if not specified in the route config.

Set the `X-Forwarded` value to a comma separated list containing the headers you need to enable. All for headers are enabled by default. All can be disabled by specifying an empty value `""`.

The Prefix specifies the header name prefix to use for each header. With the default `X-Forwarded-` prefix the resulting headers will be `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Forwarded-PathBase`.

Append specifies if each header should append to or replace an existing header of the same name. A request traversing multiple proxies may accumulate a list of such headers and the destination server will need to evaluate the list to determine the original value. If append is false and the associated value is not available on the request (e.g. RemoteIpAddress is null), any existing header is still removed to prevent spoofing.

The {Prefix}For header value is taken from `HttpContext.Connection.RemoteIpAddress` representing the prior caller's IP address. The port is not included. IPv6 addresses do not include the bounding `[]` brackets.

The {Prefix}Proto header value is taken from `HttpContext.Request.Scheme` indicating if the prior caller used HTTP or HTTPS.

The {Prefix}Host header value is taken from the incoming request's Host header. This is independent of RequestHeaderOriginalHost specified above. Unicode/IDN hosts are punycode encoded.

The {Prefix}Prefix header value is taken from `HttpContext.Request.PathBase`. The PathBase property is not used when generating the proxy request so the destination server will need the original value to correctly generate links and directs. The value is in the percent encoded Uri format.

### Forwarded

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| Forwarded | A comma separated list containing any of these values: for,by,proto,host | (none) | yes |
| ForFormat | Random/RandomAndPort/RandomAndRandomPort/Unknown/UnknownAndPort/UnknownAndRandomPort/Ip/IpAndPort/IpAndRandomPort | Random | no |
| ByFormat | Random/RandomAndPort/RandomAndRandomPort/Unknown/UnknownAndPort/UnknownAndRandomPort/Ip/IpAndPort/IpAndRandomPort | Random | no |
| Append | true/false | true | no |

Config:
```JSON
{
  "Forwarded": "by,for,host,proto",
  "ByFormat": "Random",
  "ForFormat": "IpAndPort"
},
```
Code:
```csharp
routeConfig = routeConfig.WithTransformForwarded(useHost: true, useProto: true, forFormat: NodeFormat.IpAndPort, ByFormat: NodeFormat.Random, append: true);
```
```C#
transformBuilderContext.AddForwarded(useHost: true, useProto: true, forFormat: NodeFormat.IpAndPort, ByFormat: NodeFormat.Random, append: true);
```
Example:
```
Forwarded: proto=https;host="localhost:5001";for="[::1]:20173";by=_YQuN68tm6
```

The `Forwarded` header is defined by [RFC 7239](https://tools.ietf.org/html/rfc7239). It consolidates many of the same functions as the unofficial X-Forwarded headers, flowing information to the destination server that would otherwise be obscured by using a proxy.

Enabling this transform will disable the default X-Forwarded transforms as they carry similar information in another format. The X-Forwarded transforms can still be explicitly enabled.

Append: This specifies if the transform should append to or replace an existing Forwarded header. A request traversing multiple proxies may accumulate a list of such headers and the destination server will need to evaluate the list to determine the original value.

Proto: This value is taken from `HttpContext.Request.Scheme` indicating if the prior caller used HTTP or HTTPS.

Host: This value is taken from the incoming request's Host header. This is independent of RequestHeaderOriginalHost specified above. Unicode/IDN hosts are punycode encoded.

For: This value identifies the prior caller. IP addresses are taken from `HttpContext.Connection.RemoteIpAddress`. See ByFormat and ForFormat below for details.

By: This value identifies where the proxy received the request. IP addresses are taken from `HttpContext.Connection.LocalIpAddress`. See ByFormat and ForFormat below for details.

ByFormat and ForFormat:

The RFC allows a [variety of formats](https://tools.ietf.org/html/rfc7239#section-6) for the By and For fields. It requires that the default format uses an obfuscated identifier identified here as Random.

| Format | Description | Example |
|--------|-------------|---------|
| Random | An obfuscated identifier that is generated randomly per request. This allows for diagnostic tracing scenarios while limiting the flow of uniquely identifying information for privacy reasons. | `by=_YQuN68tm6` |
| RandomAndPort | The Random identifier plus the port. | `by="_YQuN68tm6:80"` |
| RandomAndRandomPort | The Random identifier plus another random identifier for the port. | `by="_YQuN68tm6:_jDw5Cf3tQ"` |
| Unknown | This can be used when the identity of the preceding entity is not known, but the proxy server still wants to signal that the request was forwarded. | `by=unknown` |
| UnknownAndPort | The Unknown identifier plus the port if available. | `by="unknown:80"` |
| UnknownAndRandomPort | The Unknown identifier plus random identifier for the port. | `by="unknown:_jDw5Cf3tQ"` |
| Ip | An IPv4 address or an IPv6 address including brackets. | `by="[::1]"` |
| IpAndPort | The IP address plus the port. | `by="[::1]:80"` |
| IpAndRandomPort | The IP address plus random identifier for the port. | `by="[::1]:_jDw5Cf3tQ"` |

### ClientCert

| Key | Value | Required |
|-----|-------|----------|
| ClientCert | The header name | yes |

Config:
```JSON
{ "ClientCert": "X-Client-Cert" }
```
Code:
```csharp
routeConfig = routeConfig.WithTransformClientCertHeader(headerName: "X-Client-Cert");
```
```C#
transformBuilderContext.AddClientCertHeader(headerName: "X-Client-Cert");
```
Example:
```
X-Client-Cert: SSdtIGEgY2VydGlmaWNhdGU...
```

This transform causes the client certificate taken from `HttpContext.Connection.ClientCertificate` to be Base64 encoded and set as the value for the given header name. This is needed because client certificates from incoming connections are not used when making connections to the destination server. The destination server may need that certificate to authenticate the client. There is no standard that defines this header and implementations vary, check your destination server for support.

## Response and Response Trailers

All response headers and trailers are copied from the proxied response to the outgoing client response by default. Response and response trailer transforms may specify if they should be applied only for successful responses or for all responses.

In code these are implemented as derivations of the abstract classes [ResponseTransform](xref:Yarp.ReverseProxy.Transforms.ResponseTransform) and [ResponseTrailersTransform](xref:Yarp.ReverseProxy.Transforms.ResponseTrailersTransform).

### ResponseHeadersCopy

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| ResponseHeadersCopy | true/false | true | yes |

Config:
```JSON
{ "ResponseHeadersCopy": "false" }
```
Code:
```csharp
routeConfig = routeConfig.WithTransformCopyResponseHeaders(copy: false);
```
```C#
transformBuilderContext.CopyResponseHeaders = false;
```

This sets if all proxy response headers are copied to the client response. This setting is enabled by default and can be disabled by configuring the transform with a `false` value. Transforms that reference specific headers will still be run if this is disabled.

### ResponseHeader

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| ResponseHeader | The header name | (none) | yes |
| Set/Append | The header value | (none) | yes |
| When | Success/Always | Success | no |

Config:
```JSON
{
  "ResponseHeader": "HeaderName",
  "Append": "value",
  "When": "Success"
}
```
Code:
```csharp
routeConfig = routeConfig.WithTransformResponseHeader(headerName: "HeaderName", value: "value", append: true, always: false);
```
```C#
transformBuilderContext.AddResponseHeader(headerName: "HeaderName", value: "value", append: true, always: false);
```
Example:
```
HeaderName: value
```

This sets or appends the value for the named header. Set replaces any existing header. Append adds an additional header with the given value.
Note: setting "" as a header value is not recommended and can cause an undefined behavior.

`When` specifies if the response header should be included for successful responses or for all responses. Any response with a status code less than 400 is considered a success.

### ResponseHeaderRemove

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| ResponseHeaderRemove | The header name | (none) | yes |
| When | Success/Always | Success | no |

Config:
```JSON
{
  "ResponseHeaderRemove": "HeaderName",
  "When": "Success"
}
```
Code:
```csharp
routeConfig = routeConfig.WithTransformResponseHeaderRemove(headerName: "HeaderName", always: false);
```
```C#
transformBuilderContext.AddResponseHeaderRemove(headerName: "HeaderName", always: false);
```
Example:
```
HeaderName: value
AnotherHeader: another-value
```

This removes the named header.

`When` specifies if the response header should be included for successful responses or for all responses. Any response with a status code less than 400 is considered a success.

### ResponseTrailersCopy

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| ResponseTrailersCopy | true/false | true | yes |

Config:
```JSON
{ "ResponseTrailersCopy": "false" }
```
Code:
```csharp
routeConfig = routeConfig.WithTransformCopyResponseTrailers(copy: false);
```
```C#
transformBuilderContext.CopyResponseTrailers = false;
```

This sets if all proxy response trailers are copied to the client response. This setting is enabled by default and can be disabled by configuring the transform with a `false` value. Transforms that reference specific headers will still be run if this is disabled.

### ResponseTrailer

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| ResponseTrailer | The header name | (none) | yes |
| Set/Append | The header value | (none) | yes |
| When | Success/Always | Success | no |

Config:
```JSON
{
  "ResponseTrailer": "HeaderName",
  "Append": "value",
  "When": "Success"
}
```
Code:
```csharp
routeConfig = routeConfig.WithTransformResponseTrailer(headerName: "HeaderName", value: "value", append: true, always: false);
```
```C#
transformBuilderContext.AddResponseTrailer(headerName: "HeaderName", value: "value", append: true, always: false);
```
Example:
```
HeaderName: value
```

Response trailers are headers sent at the end of the response body. Support for trailers is uncommon in HTTP/1.1 implementations but is becoming common in HTTP/2 implementations. Check your client and server for support.

ResponseTrailer follows the same structure and guidance as ResponseHeader.

### ResponseTrailerRemove

| Key | Value | Default | Required |
|-----|-------|---------|----------|
| ResponseTrailerRemove | The header name | (none) | yes |
| When | Success/Always | Success | no |

Config:
```JSON
{
  "ResponseTrailerRemove": "HeaderName",
  "When": "Success"
}
```
Code:
```csharp
routeConfig = routeConfig.WithTransformResponseTrailerRemove(headerName: "HeaderName", always: false);
```
```C#
transformBuilderContext.AddResponseTrailerRemove(headerName: "HeaderName", always: false);
```
Example:
```
HeaderName: value
AnotherHeader: another-value
```

This removes the named trailing header.

ResponseTrailerRemove follows the same structure and guidance as ResponseHeaderRemove.

## Extensibility

### AddRequestTransform

[AddRequestTransform](xref:Yarp.ReverseProxy.Transforms.TransformBuilderContextFuncExtensions) is a `TransformBuilderContext` extension method that defines a request transform as a `Func<RequestTransformContext, ValueTask>`. This allows creating a custom request transform without implementing a `RequestTransform` derived class.

### AddResponseTransform

[AddResponseTransform](xref:Yarp.ReverseProxy.Transforms.TransformBuilderContextFuncExtensions) is a `TransformBuilderContext` extension method that defines a response transform as a `Func<ResponseTransformContext, ValueTask>`. This allows creating a custom response transform without implementing a `ResponseTransform` derived class.

### AddResponseTrailersTransform

[AddResponseTrailersTransform](xref:Yarp.ReverseProxy.Transforms.TransformBuilderContextFuncExtensions) is a `TransformBuilderContext` extension method that defines a response trailers transform as a `Func<ResponseTrailersTransformContext, ValueTask>`. This allows creating a custom response trailers transform without implementing a `ResponseTrailersTransform` derived class.

### RequestTransform

All request transforms must derive from the abstract base class [RequestTransform](xref:Yarp.ReverseProxy.Transforms.RequestTransform). These can freely modify the proxy `HttpRequestMessage`. Avoid reading or modifying the request body as this may disrupt the proxying flow. Consider also adding a parametrized extension method on `TransformBuilderContext` for discoverability and easy of use.

### ResponseTransform

All response transforms must derive from the abstract base class [ResponseTransform](xref:Yarp.ReverseProxy.Transforms.ResponseTransform). These can freely modify the client `HttpResponse`. Avoid reading or modifying the response body as this may disrupt the proxying flow. Consider also adding a parametrized extension method on `TransformBuilderContext` for discoverability and easy of use.

### ResponseTrailersTransform

All response trailers transforms must derive from the abstract base class [ResponseTrailersTransform](xref:Yarp.ReverseProxy.Transforms.ResponseTrailersTransform). These can freely modify the client HttpResponse trailers. These run after the response body and should not attempt to modify the response headers or body. Consider also adding a parametrized extension method on `TransformBuilderContext` for discoverability and easy of use.

### ITransformProvider

[ITransformProvider](xref:Yarp.ReverseProxy.Transforms.ITransformProvider) provides the functionality of `AddTransforms` described above as well as DI integration and validation support.

`ITransformProvider`'s can be registered in DI by calling [AddTransforms&lt;T&gt;()](xref:Microsoft.Extensions.DependencyInjection.ReverseProxyServiceCollectionExtensions). Multiple `ITransformProvider` implementations can be registered and all will be run.

`ITransformProvider` has two methods, `Validate` and `Apply`. `Validate` gives you the opportunity to inspect the route for any parameters that are needed to configure a transform, such as custom metadata, and to return validation errors on the context if any needed values are missing or invalid. The `Apply` method provides the same functionality as AddTransform as discussed above, adding and configuring transforms per route.

```C#
services.AddReverseProxy()
    .LoadFromConfig(_configuration.GetSection("ReverseProxy"))
    .AddTransforms<MyTransformProvider>();
```
```C#
internal class MyTransformProvider : ITransformProvider
{
    public void Validate(TransformValidationContext context)
    {
        // Check all routes for a custom property and validate the associated
        // transform data.
        string value = null;
        if (context.Route.Metadata?.TryGetValue("CustomMetadata", out value) ?? false)
        {
            if (string.IsNullOrEmpty(value))
            {
                context.Errors.Add(new ArgumentException(
                    "A non-empty CustomMetadata value is required")); 
            }
        }
    }

    public void Apply(TransformBuilderContext transformBuildContext)
    {
        // Check all routes for a custom property and add the associated transform.
        string value = null;
        if (transformBuildContext.Route.Metadata?.TryGetValue("CustomMetadata", out value)
            ?? false)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    "A non-empty CustomMetadata value is required");
            }

            transformBuildContext.AddRequestTransform(transformContext =>
            {
                transformContext.ProxyRequest.Headers.Add("CustomHeader", value);
                return default;
            });
        }
    }
}
```

### ITransformFactory

Developers that want to integrate their custom transforms with the `Transforms` section of configuration can implement an [ITransformFactory](xref:Yarp.ReverseProxy.Transforms.ITransformFactory). This should be registered in DI using the `AddTransformFactory<T>()` method. Multiple factories can be registered and all will be used.

`ITransformFactory` provides two methods, `Validate` and `Build`. These process one set of transform values at a time, represented by a `IReadOnlyDictionary<string, string>`.

The `Validate` method is called when loading a configuration to verify the contents and report all errors. Any reported errors will prevent the configuration from being applied.

The `Build` method takes the given configuration and produces the associated transform instances for the route.

```C#
services.AddReverseProxy()
    .LoadFromConfig(_configuration.GetSection("ReverseProxy"))
    .AddTransformFactory<MyTransformFactory>();
```
```C#
internal class MyTransformFactory : ITransformFactory
{
    public bool Validate(TransformValidationContext context,
        IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue("CustomTransform", out var value))
        {
            if (string.IsNullOrEmpty(value))
            {
                context.Errors.Add(new ArgumentException(
                    "A non-empty CustomTransform value is required"));
            }

            return true; // Matched
        }
        return false;
    }

    public bool Build(TransformBuilderContext context,
        IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue("CustomTransform", out var value))
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    "A non-empty CustomTransform value is required");
            }

            context.AddRequestTransform(transformContext =>
            {
                transformContext.ProxyRequest.Headers.Add("CustomHeader", value);
                return default;
            });

            return true; // Matched
        }

        return false;
    }
}
```

`Validate` and `Build` return `true` if they've identified the given transform configuration as one that they own. A `ITransformFactory` may implement multiple transforms. Any `RouteConfig.Transforms` entries not handled by any `ITransformFactory` will be considered configuration errors and prevent the configuration from being applied.

Consider also adding parametrized extension methods on `RouteConfig` like `WithTransformQueryValue` to facilitate programmatic route construction.

```C#
public static RouteConfig WithTransformQueryValue(this RouteConfig routeConfig, string queryKey, string value, bool append = true)
{
    var type = append ? QueryTransformFactory.AppendKey : QueryTransformFactory.SetKey;
    return routeConfig.WithTransform(transform =>
    {
        transform[QueryTransformFactory.QueryValueParameterKey] = queryKey;
        transform[type] = value;
    });
}
```
