# Yarp Ingress and Ingress Controller

This sample will walkthrough deploying a custom ingress and deploying the ingress and ingress controller to Kubernetes.

## Building the ingress container and pushing to a container registry

First, we must build a container with YARP in it.

To do this, you can either build a new .NET app referencing YARP or use the sample present here.

To build an image, run:

```
docker build . -t <REGISTRY_NAME>/yarp:1.0.0
docker push <REGISTRY_NAME>/yarp:1.0.0
```
