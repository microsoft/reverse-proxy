# Welcome to the YARP project

There is some debate whether YARP stands for "Yet Another Reverse Proxy", or "YARP a Reverse Proxy", but either way it's a project to create a reverse proxy server. You may ask whether the world needs another
reverse proxy, but we found a bunch of internal teams at Microsoft who were either building one for their service
or had been asking about APIs and tech for building one, so we decided to get them all together to work on a common solution, this project.

YARP is a reverse proxy toolkit for building fast proxy servers in C# using the infrastructure from ASP.NET and .NET Core. The key differentiator for YARP is that it's been designed to be easily customized and tweaked to match the specific needs of each deployment scenario. 

We expect YARP to ship as a library and project template that together provide a robust, performant proxy server. Its pipeline and modules are designed so that you can then customize the functionality for your needs. For example, while YARP supports configuration files, we expect that many users will want to manage the configuration programmatically based on their own backend configuration management system, YARP will provide a configuration API to enable that customization in-proc.  YARP is designed with customizability as a primary scenario, rather than requiring you to break out to script or having to rebuild from source.

# Build

Coming Soon

# Getting started

Coming Soon

# Roadmap

Coming Soon

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.