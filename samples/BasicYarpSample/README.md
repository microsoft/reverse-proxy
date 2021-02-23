# Basic YARP Sample

This sample shows how to consume the YARP Library to produce a simple reverse proxy server. 
The proxy server is implemented as middleware for the ASP.NET Core "Kestrel" web server. Kestrel provies the front end for the proxy, which listens for http requests, and then passes them to the proxy for paths that it has registered for. The proxy handled the requests by:
* Mapping the path to a route
* Routes are mapped to clusters which are a collection of destination endpoints.
* The destinations are filtered based on health status, and session affinity (not used in this sample)
* From the remaining destinations, one is selected using a load balancing algorithm
* And the request is proxied to that destination.

This sample reads its configuration from the [appsettings.json](appsettings.json) file which defines 2 routes and clusters:

* "AnExample" - this route has a path of "{**catch-all}" which means that it will match any path, unless there is a another more specific route.
   It routes to a cluster named "example" which has a single destination of "http://example.com"
* "route2" - this route matches a path of "/something/{*any}" so will match any path that begins with "/something/". 
As its a more specific route, it will match before the route above, even though its listed second. 
This routes to a cluster named "cluster2" with 2 destinations. 
It will load balance between those destinations using a Power of two choices algorithm. 
That algorithm is best with more than 2 choices, but shows how to specify an algorithm in config.

**Note:** The addresses used in the sample are using DNS names rather than IP addresses, this is so that the sample can be run and used without further changes. In a typical deployment, the destination servers should be specified with protocol, IP & ports, such as "https://123.4.5.6:7890"

The proxy will listen to http requests on port 5000, and https on port 5001. These are changable via the URLs property in config, and can be limited to just one protocol if required.

## Files
* [BasicYarpSample.csproj](basicyarpsample.csproj) - A C# project file (conceptually similar to a make file) that tells it to target .NET 5 runtime, and to reference the proxy library from [nuget](https://www.nuget.org/packages/Microsoft.ReverseProxy/).
* [Program.cs](program.cs) - Provides the main entrypoint for .NET which uses an WebHostBuilder to initialize the kestrel server which listens for http requests. Typically, this file does not need to be modified for any proxy scenarios.
* [Startup.cs](startup.cs) - Provides a class that is used to configure and control how http requests are handled by kestrel. In this sample, it does the bare minimal of:
  * Adding proxy functionality to the service
  * Specifying that the proxy configuration will come from the config file - it could be specified via code instead
  * Telling kestrel to use its routing service, to register the routes from the proxy into its routing table, and use the proxy middleware to handle those requests.
* [appsettings.json](appsettings.json) - The configuration file for the .NET app, including sections for Kestrel, logging and the proxy configuration. 
* [Properties/launchsettings.json](Properties/launchsettings.json) - a configuration file use by Visual Studio to tell it how to start the app when debugging.

## Getting started

### Command line

* Download and install the .NET SDK (Free) from https://dotnet.microsoft.com/download if not already installed. Versions are available for Windows, Linux and MacOS.
* Clone or extract a zip of the sample files
* Use ```dotnet run``` either within the sample folder or passing in the path to the .csproj file to start the server.
* File change notification is used for the appsettings.config file so changes can be made on the fly.


### Visual Studio Code
* Download and install Visual Studio Code (Free) from https://code.visualstudio.com/. Versions are available for Windows, Linux and MacOS
* Download and install the .NET SDK from https://dotnet.microsoft.com/download if not already installed. Versions are available for Windows, Linux and MacOS.
* Open the folder for the sample in VS Code (File->Open Folder)
* Press F5 to debug, or Ctrl + F5 to run the sample without debugging.

### Visual Studio

* Download and install Visual Studio from https://visualstudio.microsoft.com/ - versions are available for Windows & MacOS including a free community edition.
* Open the project file
* Press F5 to debug, or Ctrl + F5 to run the sample without debugging.

## Things to try
 * Change the ports the proxy listens on using the URLs property in configuration
 * Change the routes and destinations used by the proxy
 * A web server sample is available in the [sampleserver](../sampleserver) folder. It will output the request headers as part of the response body so they can be examined with a browser. 
   * The urls the server listens to can be changed on the cmd line, so that multiple instances can be run. eg ```dotnet run ../SampleServer --Urls "http://localhost:10000;https://localhost:100010"```
