# YARP Direct Proxy Example

The YARP Proxy Library includes a pipeline which provides an implementation of Routes, Clusters and Destinations, and will figure out an applicable destination endpoint for a request. 
Some customers who already have a custom proxy solution have implementations of these concepts, but are coming to YARP for its support of HTTP/2, gRPC and modern protocols that are more complex to implement than HTTP/1.1.
To enable these scenarios, the actual proxy operation from YARP is exposed via the HttpProxy class, which can be called directly.

This sample shows how to use the ProxyAsync method to proxy requests and responses to a supplied destination. Part of the proxy operation is to transform the headers between the incoming and outbound request and vice versa for the response. The HttpTransformer can be derived from to customize the header transformation.

All the significant implementation can be found in [Startup.cs](Startup.cs).