---
uid: header-routing
title: Header Routing
---

# Header Based Routing

Proxy routes specified in [config](configfiles.md) or via [code](configproviders.md) must include at least a path or host to match against. In addition to these, a route can also specify one or more headers that must be present on the request.

### Precedence

The default route match precedence order is 1) path, 2) method, 3) host, 4) headers. That means a route which specifies methods and no headers will match before a route which specifies headers and no methods. This can be overridden by setting the `Order` property on a route.

## Configuration

Headers are specified in the `Match` section of a proxy route.

If multiple headers rules are specified on a route then all must match for a route to be taken. OR logic must be implemented either within a header rule or as separate routes.

Configuration:
```JSON
    "Routes": {
      "route1" : {
        "ClusterId": "cluster1",
        "Match": {
          "Path": "{**catch-all}",
          "Headers": [
            {
              "Name": "header1",
              "Values": [ "value1" ],
              "Mode": "ExactHeader"
            }
          ]
        }
      },
      "route2" : {
        "ClusterId": "cluster1",
        "Match": {
          "Path": "{**catch-all}",
          "Headers": [
            {
              "Name": "header2",
              "Values": [ "1prefix", "2prefix" ],
              "Mode": "HeaderPrefix"
            }
          ]
        }
      },
      "route3" : {
        "ClusterId": "cluster1",
        "Match": {
          "Path": "{**catch-all}",
          "Headers": [
            {
              "Name": "header3",
              "Mode": "Exists"
            }
          ]
        }
      },
      "route4" : {
        "ClusterId": "cluster1",
        "Match": {
          "Path": "{**catch-all}",
          "Headers": [
            {
              "Name": "header4",
              "Values": [ "value1", "value2" ],
              "Mode": "ExactHeader"
            },
            {
              "Name": "header5",
              "Mode": "Exists"
            }
          ]
        }
      }
    }
```

Code:
```C#
    var routes = new[]
    {
        new ProxyRoute()
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new ProxyMatch
            {
                Path = "{**catch-all}",
                Headers = new[]
                {
                    new RouteHeader()
                    {
                        Name = "Header1",
                        Values = new[] { "value1" },
                        Mode = HeaderMatchMode.ExactHeader
                    }
                }
            }
        },
        new ProxyRoute()
        {
            RouteId = "route2",
            ClusterId = "cluster1",
            Match = new ProxyMatch
            {
                Path = "{**catch-all}",
                Headers = new[]
                {
                    new RouteHeader()
                    {
                        Name = "Header2",
                        Values = new[] { "1prefix", "2prefix" },
                        Mode = HeaderMatchMode.HeaderPrefix
                    }
                }
            }
        },
        new ProxyRoute()
        {
            RouteId = "route3",
            ClusterId = "cluster1",
            Match = new ProxyMatch
            {
                Path = "{**catch-all}",
                Headers = new[]
                {
                    new RouteHeader()
                    {
                        Name = "Header3",
                        Mode = HeaderMatchMode.Exists
                    }
                }
            }
        },
        new ProxyRoute()
        {
            RouteId = "route4",
            ClusterId = "cluster1",
            Match = new ProxyMatch
            {
                Path = "{**catch-all}",
                Headers = new[]
                {
                    new RouteHeader()
                    {
                        Name = "Header4",
                        Values = new[] { "value1", "value2" },
                        Mode = HeaderMatchMode.ExactHeader
                    },
                    new RouteHeader()
                    {
                        Name = "Header5",
                        Mode = HeaderMatchMode.Exists
                    }
                }
            }
        }
    };
```

## Contract

[RouteHeader](xref:Yarp.ReverseProxy.Abstractions.RouteHeader) defines the code contract and is mapped from config.

### Name

The header name to check for on the request. A non-empty value is required. This field is case-insensitive per the HTTP RFCs.

### Values

A list of possible values to search for. The header must match at least one of these values according to the specified `Mode`. At least one value is required unless `Mode` is set to `Exists`.

### Mode

[HeaderMatchMode](xref:Yarp.ReverseProxy.Abstractions.HeaderMatchMode) specifies how to match the value(s) against the request header. The default is `ExactHeader`.
- ExactHeader - The header must match in its entirety, subject to the value of `IsCaseSensitive`. Only single headers are supported. If there are multiple headers with the same name then the match fails.
- HeaderPrefix - The header must match by prefix, subject to the value of `IsCaseSensitive`. Only single headers are supported. If there are multiple headers with the same name then the match fails.
- Exists - The header must exist and contain any non-empty value.

### IsCaseSensitive

Indicates if the value match should be performed as case sensitive or insensitive. The default is `false`, insensitive.

## Examples

These examples use the configuration specified above.

### Scenario 1 - Exact Header Match

A request with the following header will match against route1.
```
Header1: Value1
```

A header with multiple values is not currently supported and will _not_ match.
```
Header1: Value1, Value2
```

Multiple headers with the same name are not currently supported and will _not_ match.
```
Header1: Value1
Header1: Value2
```

### Scenario 2 - Multiple Values

Route2 defined multiple values to search for in a header ("1prefix", "2prefix"), any of the values are acceptable. It also specified the `Mode` as `HeaderPrefix`, so any header that starts with those values is acceptable.

Any of the following headers will match route2.
```
Header2: 1prefix
```
```
Header2: 2prefix
```
```
Header2: 1prefix-extra
```
```
Header2: 2prefix-extra
```

A header with multiple values is not currently supported and will _not_ match.
```
Header2: 1prefix, 2prefix
```

Multiple headers with the same name are not currently supported and will _not_ match.
```
Header2: 1prefix
Header2: 2prefix
```

### Scenario 3 - Exists

Route3 only requires that the header "Header3" exists with any non-empty value

The following is an example that will match route3.
```
Header3: value
```

An empty header will _not_ match.
```
Header3:
```

This mode does support headers with multiple values and multiple headers with the same name since it does not look at the header contents. The following will match.
```
Header3: value1, value2
```
```
Header3: value1
Header3: value2
```

### Scenario 4 - Multiple Headers

Route4 requires both `header4` and `header5`, each matching according to their specified `Mode`. The following headers will match route4:
```
Header4: value1
Header5: AnyValue
```
```
Header4: value2
Header5: AnyValue
```

These will _not_ match route4 because they are missing one of the required headers:
```
Header4: value2
```
```
Header5: AnyValue
```
