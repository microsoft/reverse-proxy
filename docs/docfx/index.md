---
uid: root
title: YARP Documentation
---

# YARP: Yet Another Reverse Proxy

Welcome to the documentation for YARP! YARP is a library to help create reverse proxy servers that are high-performance, production-ready, and highly customizable. Please provide us your feedback by going to [the GitHub repository](https://github.com/microsoft/reverse-proxy).

This is the documentation for YARP 2.2.
For documentation of YARP 1.1.1, see https://github.com/microsoft/reverse-proxy/tree/release/1.1/docs/docfx/articles.

## Why YARP

We found a bunch of internal teams at Microsoft who were either building a reverse proxy for their service or had been asking about APIs and tech for building one, so we decided to get them all together to work on a common solution, this project. Each of these projects was doing something slightly off the beaten path which meant they were not well served by existing proxies, and customization of those proxies had a high cost and ongoing maintenance considerations.

Many of the existing proxies were built to support HTTP/1.1, but with workloads changing to include gRPC traffic, they require HTTP/2 support which requires a significantly more complex implementation. By using YARP the projects get to customize the routing and handling behavior without having to implement the http protocol.

## Using YARP

YARP is built on .NET using the infrastructure from ASP.NET and .NET (.NET 6 and newer). The key differentiator for YARP is that it's been designed to be easily customized and tweaked via .NET code to match the specific needs of each deployment scenario.

Eventually we expect YARP to ship as a library, project template, and a single-file exe, to provide a variety of choices for building a robust, performant proxy server. Its pipeline and modules are designed so that you can then customize the functionality for your needs. For example, while YARP supports configuration files, we expect that many users will want to manage the configuration programmatically based on their own configuration management system. YARP provides a configuration API to enable that customization in-proc. 

YARP is designed with customizability as a primary scenario rather than requiring you to break out to script or rebuild the library from source.

See the [Getting Started](articles/getting-started.md) guide for a brief tutorial, or [Basic Sample](https://github.com/microsoft/reverse-proxy/tree/main/samples/BasicYarpSample) for a fully commented sample showing how to use the YARP library to implement a fairly well featured proxy.
