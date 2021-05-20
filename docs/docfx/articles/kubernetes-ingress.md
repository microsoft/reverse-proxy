# Kubernetes Ingress Controller

Introduced: 1.0.0

YARP can be integrated with Kubernetes as a reverse proxy managing HTTP/HTTPS traffic ingress to a Kubernetes cluster. Currently, the module is shipped as a separate package and is in preview.

## Prerequisites

Before we continue with this tutorial, make sure you have the following ready...

1. Installing [docker](https://docs.docker.com/install/) based on your operating system.

2. A container registry. Docker by default will create a container registry on [DockerHub](https://hub.docker.com/). You could also use [Azure Container Registry](https://docs.microsoft.com/en-us/azure/aks/tutorial-kubernetes-prepare-acr) or another container registry of your choice, like a [local registry](https://docs.docker.com/registry/deploying/#run-a-local-registry) for testing.

3. A Kubernetes Cluster. There are many different options here, including:
    - [Azure Kubernetes Service](https://docs.microsoft.com/en-us/azure/aks/tutorial-kubernetes-deploy-cluster)
    - [Kubernetes in Docker Desktop](https://www.docker.com/blog/docker-windows-desktop-now-kubernetes/), however it does take up quite a bit of memory on your machine, so use with caution.
    - [Minikube](https://kubernetes.io/docs/tasks/tools/install-minikube/)
    - [K3s](https://k3s.io), a lightweight single-binary certified Kubernetes distribution from Rancher.
    - Another Kubernetes provider of your choice.

> :warning: If you choose a container registry provided by a cloud provider (other than Dockerhub), you will likely have to take some steps to configure your kubernetes cluster to allow access. Follow the instructions provided by your cloud provider.

## Get Started

The first step will be to deploy the YARP ingress controller to the Kubernetes cluster. This can be done by navigating to [Kubernetes Ingress sample](..\..\..\samples\KuberenetesIngress.Sample\Ingress) and running:

```
kubectl apply -f ingress-definition.yaml
```

To verify that the ingress controller has been deployed, run:

```
kubectl get pods -n yarp
```

You can then check logs from the ingress controller by running:

```
kubectl logs <POD NAME> -n yarp
```

> :bulb: All services, deployments, and pods for YARP are in the namespace 'yarp'. Make sure to include '-n yarp' if you want to check on the status of yarp.

Next, we need to build and deploy our ingress. In the root

```
docker build ../../../ -t <REGISTRY_NAME>/yarp:1.0.0 -f .\Dockerfile
docker push <REGISTRY_NAME>/yarp:1.0.0
```

where REGISTRY_NAME is the name of your docker registry.
