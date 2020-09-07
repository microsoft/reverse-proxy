---
uid: root
title: YARP Documentation
---

# Yarp, Another Reverse Proxy!

Welcome to the documentation for YARP! YARP is a library to help create reverse proxy servers that are high-performance, production-ready, and highly customizable. Right now it's still in preview, but please provide us your feedback by going to [the GitHub repository](https://github.com/microsoft/reverse-proxy).

We found a bunch of internal teams at Microsoft who were either building a reverse proxy for their service or had been asking about APIs and tech for building one, so we decided to get them all together to work on a common solution, this project.

YARP is built on .NET using the infrastructure from ASP.NET and .NET (.NET Core 3.1 and .NET 5.0). The key differentiator for YARP is that it's been designed to be easily customized and tweaked via .NET code to match the specific needs of each deployment scenario. 

We expect YARP to ship as a library, project template, and a single-file exe, to provide a variety of choices for building a robust, performant proxy server. Its pipeline and modules are designed so that you can then customize the functionality for your needs. For example, while YARP supports configuration files, we expect that many users will want to manage the configuration programmatically based on their own configuration management system, YARP will provide a configuration API to enable that customization in-proc. YARP is designed with customizability as a primary scenario rather than requiring you to break out to script or rebuild the library from source.

See the [Getting Started](xref:getting_started) guide for a brief tutorial.
